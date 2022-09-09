using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Qs.EventGrid.Emulator;

class EventProcessor : BackgroundService
{
    public static Queue<(EventGridEvent, Subscription, int)> Events = new();

    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        var (eventTypeMap, eventGridClient, logger) = ctor;
        logger.LogDebug($"EventProcessor is starting.");
        cancel.Register(() => logger.LogDebug($"EventProcessor background task is stopping."));
        var dequeueErrCt = 0;

        while (!cancel.IsCancellationRequested)
        {
            (EventGridEvent @event, Subscription subscription, int attempt, string id, string type) = (null, null, -1, null, null);

            if (dequeueErrCt > 0) logger.LogTrace("Looking for events (ErrorCt:{ErrorCt})", dequeueErrCt);

            try
            {
                if (!Events.TryDequeue(out var item))
                {
                    dequeueErrCt = 0;
                    await Task.Delay(1000, cancel);
                    continue;
                }
                (@event, subscription, attempt) = item;
                (dequeueErrCt, id, type) = (0, @event.Id, @event.EventType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error({ErrorCt}): Failed to dequeue event.", ++dequeueErrCt);
                await Task.Delay(dequeueErrCt * 5000, cancel);
                continue;
            }

            try
            {
                var endpoint = await eventGridClient.SendEventAsync(subscription.Service.BaseAddress, subscription.Endpoint, @event);
                logger.LogInformation("Event pushed {Id}/{Attempt} {EventType} {Subject} to {Subscription}", id, attempt, type, @event.Subject, subscription);
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= 7)
                {
                    //todo: use a DLQ (short TTL, eg- 24h)? eg- az storage emulator
                    logger.LogWarning("Error: {Error}. Event {Id}/{Attempt} deadlettered (to log) for {Subscription} :\n{Event}", ex.Message, id, attempt, subscription, @event.ToJson());
                    continue;
                }

                logger.LogInformation("Error: {Error}. Event delivery for {Id}/{Attempt} will be reattempted for {Subscription}.", ex.Message, id, attempt, subscription);
                var _ = Task.Run(DelayedRequeue, cancel).ConfigureAwait(false); // run on thread pool

                async Task DelayedRequeue()
                {
                    await ExponentialBackOff(attempt, id);
                    if (cancel.IsCancellationRequested) return;
                    Events.Enqueue((@event, subscription, attempt + 1));
                    logger.LogDebug("Event {Id}/{Attempt} requeued & available for {Subscriber}.", id, attempt + 1, subscription);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: Event processing failed. Event {Id}/{Attempt} deadlettered (to log) for {Subscription}:\n{Event}", id, attempt, subscription, @event.ToJson());
            }

            await Task.Delay(500, cancel); // short wait on poller loop

            async Task ExponentialBackOff(int times, string eventId, [CallerMemberName] string method = null, [CallerLineNumber] int? line = null)
            {
                var delayMs = Math.Clamp((int)Math.Pow(4, times), 1, 30 * 60) * 1000; // number->secs : 1->4s, 2->16s, 3->64s, 4->4m16s, 5->17m4s, 6+->30m
                logger.LogDebug("ExponentialBackOff:{Times} of {Delay} for event {EventId} ({Method}:{Line}).", times, TimeSpan.FromMilliseconds(delayMs), eventId, method, line);
                await Task.Delay(delayMs, cancel);
            }
        }

        logger.LogInformation($"EventProcessor background task has finished.");
    }

    public EventProcessor(IEventGridClient eventGridClient, IOptions<Services> options, ILogger<EventProcessor> logger)
        => ctor = (options.Value.KeyByEventType(), eventGridClient, logger);
    readonly (EventTypeMap eventTypeMap, IEventGridClient eventGridClient, ILogger logger) ctor;
}
