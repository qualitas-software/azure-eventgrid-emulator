using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
                logger.LogTrace("Looking for events ({errors})", errorCt);

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
                    logger.LogError(ex, "Failed to dequeue event [{id}/{errors}].", id, errorCt);
                    await Task.Delay(1000 * (int)Math.Pow(3, errorCt++), cancel);
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

                    logger.LogInformation($"Event pushed {item.@event.Id}/{item.attempt} {item.@event.EventType} {item.@event.Subject}");
                    errorCt = 0;
                }
                catch (HttpRequestException ex)
                {
                    if (item.attempt >= 5)
                    {
                        logger.LogWarning("Error({errorCt}): {error}. Event {id}/{attempt} deadlettered:\n{event}", errorCt, ex.Message, id, item.attempt, item.@event.ToJson());
                        continue;
                    }

                    logger.LogInformation("Error({errorCt}): {error}. Event delivery for {id}/{attempt} will be reattempted.", errorCt, ex.Message, id, item.attempt);
                    var q = Task.Run(async () =>
                    {
                        await Task.Delay(delayMs(2 + item.attempt), cancel);
                        if (cancel.IsCancellationRequested) return;
                        Queue.Enqueue((item.@event, item.attempt + 1));
                        logger.LogDebug("Event {id}/{attempt} requeued.", id, item.attempt + 1);
                    }, cancel).ConfigureAwait(false);

                    logger.LogDebug("Sleep after error starting for {delay}ms.", delayMs(errorCt));
                    await Task.Delay(delayMs(errorCt++), cancel);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error({errors}): Event processing failed. Event dropped:\n{Event}", errorCt, item.@event.ToJson());
                    await Task.Delay(delayMs(errorCt++), cancel);
                }

                static int delayMs(int number) => ((int)Math.Pow(2, number > 7 ? 7 : number)) * 1000; // number->secs : 1->2s, 2->4s, 3->8s, 4->16s, 5->32s, 6->64s, 7->128s
            }

            logger.LogInformation($"EventProcessor background task has finished.");
        }

        public EventProcessor(IEventGridClient subscriber, ILogger<EventProcessor> logger)
            => ctor = (subscriber, logger);
        readonly (IEventGridClient subscriber, ILogger<EventProcessor> logger) ctor;
    }
}
