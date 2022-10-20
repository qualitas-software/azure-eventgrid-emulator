using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Qs.EventGrid.Emulator;

class EventProcessor : BackgroundService
{
    public static Queue<(EventGridEvent, Subscription, int, DateTime)> Events = new();

    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        var (eventTypeMap, eventGridClient, storageClient, logger) = ctor;
        logger.LogDebug("EventProcessor is starting for {EventTypes} event types.", eventTypeMap.Count);
        cancel.Register(() => logger.LogInformation($"EventProcessor background task is stopping."));
        int dequeueErrorCt = 0, dequeueEmptyCt = 0;

        while (!cancel.IsCancellationRequested)
        {
            (EventGridEvent @event, Subscription subscription, int attempt, DateTime receivedUtc, string id, string type) = (null, null, -1, default, null, null);

            try
            {
                logger.LogTrace("Checking for events {{EmptyCt:{EmptyCt},{ErrorCt}}}", dequeueEmptyCt, ++dequeueErrorCt);

                if (!Events.TryDequeue(out var item))
                {
                    dequeueErrorCt = 0;
                    await Task.Delay(CalcExponentialBackOffMs(++dequeueEmptyCt, 2, 20), cancel);
                    continue;
                }

                (@event, subscription, attempt, receivedUtc) = item;
                (dequeueEmptyCt, dequeueErrorCt, id, type) = (0, 0, @event.Id, @event.EventType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error({ErrorCt}): Failed to dequeue event. Polling will backoff.", ++dequeueErrorCt);
                await Task.Delay(CalcExponentialBackOffMs(dequeueErrorCt), cancel);
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
                    var deadletterMsgId = await storageClient.EnqueueDeadletteredEventAsync(@event, $"Too many attempts. {ex.Message}", subscription, attempt, receivedUtc, logger);
                    logger.LogWarning("Error: {Error}. Event {Id}/{Attempt} deadlettered as {DeadletterMsgId} for {Subscription} :\n{Event}", ex.Message, id, attempt, deadletterMsgId, subscription, @event.ToJson());
                    continue;
                }

                logger.LogInformation("Error: {Error}. Event delivery for {Id}/{Attempt} will be reattempted for {Subscription}.", ex.Message, id, attempt, subscription);
                var _ = Task.Run(DelayedRequeue, cancel).ConfigureAwait(false); // run on thread pool

                async Task DelayedRequeue()
                {
                    await ExponentialBackOff(attempt, id);
                    if (cancel.IsCancellationRequested) return;
                    Events.Enqueue((@event, subscription, attempt + 1, receivedUtc));
                    logger.LogDebug("Event {Id} requeued as attempt {Attempt} after {SinceFirstReceived} & available for {Subscriber}.", id, attempt + 1, (DateTime.UtcNow - receivedUtc).Trim(), subscription);
                }
            }
            catch (Exception ex)
            {
                var deadletterMsgId = await storageClient.EnqueueDeadletteredEventAsync(@event, $"{ex.Message} ({ex.GetType().Name})", subscription, attempt, receivedUtc, logger);
                logger.LogError(ex, "Error: Event processing failed. Event {Id}/{Attempt} deadlettered as {DeadletterMsgId} for {Subscription}:\n{Event}", id, attempt, deadletterMsgId, subscription, @event.ToJson());
            }

            await Task.Delay(10, cancel); // short wait on completion of poller loop

            async Task ExponentialBackOff(int times, string eventId, [CallerMemberName] string method = null, [CallerLineNumber] int? line = null)
            {
                var delayMs = CalcExponentialBackOffMs(times);
                logger.LogDebug("ExponentialBackOff:{Times} of {Delay} for event {EventId} ({Method}:{Line}).", times, TimeSpan.FromMilliseconds(delayMs), eventId, method, line);
                await Task.Delay(delayMs, cancel);
            }

            static int CalcExponentialBackOffMs(int exponent, int baseSecs = 4, int maxSecs = 10 * 60)
                => Math.Clamp((int)Math.Pow(baseSecs, exponent), 1, maxSecs) * 1000; // 1=>4s, 2=>16s, 3=>64s, 4=>4m16s, 5=>17m4s, 6+=>30m
        }

        logger.LogInformation($"EventProcessor background task has finished.");
    }

    public EventProcessor(IEventGridClient eventGridClient, IOptions<Services> options, StorageClient storageClient, ILogger<EventProcessor> logger)
        => ctor = (options.Value.KeyByEventType(), eventGridClient, storageClient, logger);
    readonly (EventTypeMap eventTypeMap, IEventGridClient eventGridClient, StorageClient storageClient, ILogger logger) ctor;
}
