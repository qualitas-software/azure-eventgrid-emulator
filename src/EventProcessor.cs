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
        var errorCt = 0;

        while (!cancel.IsCancellationRequested)
        {
            (EventGridEvent @Event, Subscription Subscription, int Attempt) item = (null, null, -1);
            string id = null, type = null;

            logger.LogTrace("Looking for events ({Errors})", errorCt);

            try
            {
                if (!Events.TryDequeue(out item))
                {
                    await Task.Delay(500, cancel);
                    continue;
                }
                (id, type) = (item.Event.Id, item.Event.EventType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error({ErrorCt}): Failed to dequeue event.", errorCt);
                await ExponentialBackOff();
                continue;
            }

            try
            {
                // Get correct client & push the event to the correct endpoint (eg- WebHook or Azure Function)
                var endpoint = await eventGridClient.SendEventAsync(item.Subscription.Service.BaseAddress, item.Subscription.Endpoint, item.Event);

                logger.LogInformation("Event pushed {Id}/{Attempt} {EventType} {Subject} to {Subscription}", id, item.Attempt, type, item.Event.Subject, item.Subscription);
                errorCt = 0;
            }
            catch (HttpRequestException ex)
            {
                if (item.Attempt >= 5)
                {
                    //todo: use a DLQ (short TTL, eg- 24h)? eg- az storage emulator
                    logger.LogWarning("Error({ErrorCt}): {Error}. Event {Id}/{Attempt} deadlettered (to log) for {Subscription} :\n{Event}", errorCt, ex.Message, id, item.Attempt, item.Subscription, item.Event.ToJson());
                    continue;
                }

                logger.LogInformation("Error({ErrorCt}): {Error}. Event delivery for {Id}/{Attempt} will be reattempted for {Subscription}.", errorCt, ex.Message, id, item.Attempt, item.Subscription);
                var _ = Task.Run(DelayedRequeue, cancel).ConfigureAwait(false); // run on thread pool

                async Task DelayedRequeue()
                {
                    await ExponentialBackOff(2 + item.Attempt);
                    if (cancel.IsCancellationRequested) return;
                    Events.Enqueue((item.Event, item.Subscription, item.Attempt + 1));
                    logger.LogDebug("Event {Id}/{Attempt} requeued & available for {Subscriber}.", id, item.Attempt + 1, item.Subscription);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error({ErrorCt}): Event processing failed. Event {Id}/{Attempt} deadlettered (to log) for {Subscription}:\n{Event}", errorCt, id, item.Attempt, item.Subscription, item.Event.ToJson());
            }

            await Task.Delay(500, cancel); // short wait on poller loop

            async Task ExponentialBackOff(int? times = null, [CallerMemberName] string method = null, [CallerLineNumber] int? line = null)
            {
                times ??= errorCt++; // use & increment errorCt when not supplied
                var delayMs = ((int)Math.Pow(2, times.Value > 7 ? 7 : times.Value)) * 1000; // number->secs : 1->2s, 2->4s, 3->8s, 4->16s, 5->32s, 6->64s, 7->128s
                logger.LogDebug("Sleep after error starting for {DelayMs}ms for {Method}:{Line}.", delayMs, method, line);
                await Task.Delay(delayMs, cancel);
            }
        }

        logger.LogInformation($"EventProcessor background task has finished.");
    }

    public EventProcessor(IEventGridClient eventGridClient, IOptions<Services> options, ILogger<EventProcessor> logger)
        => ctor = (options.Value.KeyByEventType(), eventGridClient, logger);
    readonly (EventTypeMap eventTypeMap, IEventGridClient eventGridClient, ILogger logger) ctor;
}
