using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Qs.EventGrid.Emulator
{
    class EventProcessor : BackgroundService
    {
        public static Queue<(EventGridEvent, int)> Queue = new();

        protected override async Task ExecuteAsync(CancellationToken cancel)
        {
            var (subscriber, logger) = ctor;
            logger.LogDebug($"EventProcessor is starting.");
            cancel.Register(() => logger.LogDebug($"EventProcessor background task is stopping."));
            var errorCt = 0;

            while (!cancel.IsCancellationRequested)
            {
                (EventGridEvent @event, int attempt) item = (null, -1); string id = null;
                logger.LogTrace("Looking for events ({Errors})", errorCt);

                try
                {
                    if (Queue.TryDequeue(out item)) id = item.@event.Id;
                    else
                    {
                        await Task.Delay(1000, cancel);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error({ErrorCt}): Failed to dequeue event.", errorCt);
                    await ExponentialBackOff();
                    continue;
                }

                try
                {
                    // Push the event to the correct local endpoint (eg- Azure Function name)  //todo: make mapping config driven
                    await (item.@event.EventType switch
                    {
                        nameof(EventTypes.CreateRequest) => subscriber.SendEventAsync("CreateEventHandler", item.@event),
                        nameof(EventTypes.UpdateRequest) => subscriber.SendEventAsync("UpdateEventHandler", item.@event),
                        nameof(EventTypes.Progress) => subscriber.SendEventAsync("ProgressEventHandler", item.@event),
                        _ => throw new ArgumentException($"Event type '{item.@event.EventType}' not supported.")
                    });

                    logger.LogInformation("Event pushed {Id}/{Attempt} {EventType} {Subject}", id, item.attempt, item.@event.EventType, item.@event.Subject);
                    errorCt = 0;
                }
                catch (HttpRequestException ex)
                {
                    if (item.attempt >= 5)
                    {
                        //todo: use a DLQ (short TTL, eg- 24h)? eg- az storage emulator
                        logger.LogWarning("Error({ErrorCt}): {Error}. Event {Id}/{Attempt} deadlettered (to log):\n{Event}", errorCt, ex.Message, id, item.attempt, item.@event.ToJson());
                        continue;
                    }

                    logger.LogInformation("Error({ErrorCt}): {Error}. Event delivery for {Id}/{Attempt} will be reattempted.", errorCt, ex.Message, id, item.attempt);
                    var q = Task.Run(DelayedRequeue, cancel).ConfigureAwait(false);
                    await ExponentialBackOff();
                    
                    async Task DelayedRequeue()
                    {
                        await ExponentialBackOff(2 + item.attempt);
                        if (cancel.IsCancellationRequested) return;
                        Queue.Enqueue((item.@event, item.attempt + 1));
                        logger.LogDebug("Event {Id}/{Attempt} requeued.", id, item.attempt + 1);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error({ErrorCt}): Event processing failed. Event {Id}/{Attempt} deadlettered (to log):\n{Event}", errorCt, id, item.attempt, item.@event.ToJson());
                    await ExponentialBackOff();
                }

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

        public EventProcessor(IEventGridClient subscriber, ILogger<EventProcessor> logger)
            => ctor = (subscriber, logger);
        readonly (IEventGridClient subscriber, ILogger<EventProcessor> logger) ctor;
    }
}
