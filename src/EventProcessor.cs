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
        public static Queue<EventGridEvent> Queue = new();

        protected override async Task ExecuteAsync(CancellationToken cancel)
        {
            ctor.logger.LogDebug($"EventProcessor is starting.");
            cancel.Register(() => ctor.logger.LogDebug($"EventProcessor background task is stopping."));
            var errors = 0;

            while (!cancel.IsCancellationRequested)
            {
                EventGridEvent @event = null; string id = null;

                try
                {
                    if (Queue.TryDequeue(out @event)) id = @event.Id;
                    else
                    {
                        await Task.Delay(1000, cancel);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    ctor.logger.LogError(ex, "EventProcessor : Failed to dequeue event [{id}/{errors}].", id, errors);
                    await Task.Delay(1000 * (int)Math.Pow(3, errors++), cancel);
                    continue;
                }

                try
                {
                    // Push the event to the correct local endpoint (eg- Azure Function name)  //todo: make mapping config driven
                    await (@event.EventType switch
                    {
                        nameof(EventTypes.CreateRequest) => ctor.subscriber.SendEventAsync("CreateEventHandler", @event),
                        nameof(EventTypes.UpdateRequest) => ctor.subscriber.SendEventAsync("UpdateEventHandler", @event),
                        nameof(EventTypes.Progress) => ctor.subscriber.SendEventAsync("ProgressEventHandler", @event),
                        _ => throw new ArgumentException($"Event type '{@event.EventType}' not supported.")
                    });

                    ctor.logger.LogInformation($"Event pushed {@event.Id} {@event.EventType} {@event.Subject}");
                    errors = 0;
                }
                catch (HttpRequestException ex)
                {
                    ctor.logger.LogWarning("EventProcessor : {error} [{id}/{errors}]. Event delivery will be reattempted.", ex.Message, id, errors);
                    var q = Task.Run(async () => { await Task.Delay(60 * 1000, cancel); if (!cancel.IsCancellationRequested) Queue.Enqueue(@event); }, cancel);
                    await Task.Delay(1000 * (int)Math.Pow(3, errors++), cancel);
                }
                catch (Exception ex)
                {
                    ctor.logger.LogError(ex, "EventProcessor [{id}/{errors}]. Event dropped:\n{Event}", id, errors, @event.ToJson());
                    await Task.Delay(1000 * (int)Math.Pow(3, errors++), cancel);
                }
            }

            ctor.logger.LogDebug($"EventProcessor background task is finished.");
        }

        public EventProcessor(IEventGridClient subscriber, ILogger<EventProcessor> logger) //todo: support >1 destination app via the client
            => ctor = (subscriber, logger);
        readonly (IEventGridClient subscriber, ILogger<EventProcessor> logger) ctor;
    }
}
