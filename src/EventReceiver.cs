using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Qs.EventGrid.Emulator;

using static Constants;
using static JsonHelpers;

class EventReceiver
{
    public async Task ReceiveAsync(HttpContext context)
    {
        var (eventTypeMap, storageClient, logger) = ctor;
        context.Response.Headers.Add("App", Namespace);

        try
        {
            var json = await context.Request.ReadFromJsonAsync<JsonDocument>(JsonSerializerOptions);

            try
            {
                var @events = json.FromJson<List<EventGridEvent>>();

                foreach (var @event in events)
                {
                    if (!eventTypeMap.TryGetValue(@event.EventType, out var subscriptions))
                    {
                        logger.LogError("Error: No subscribers setup for {EventType} event type.  Event dropped: {Event}.", @event.EventType, @event.ToJson(true));
                        await storageClient.EnqueueReceivedEventAsync(@event);
                        continue;
                    }

                    await storageClient.EnqueueReceivedEventAsync(@event, subscriptions);

                    foreach (var subscription in subscriptions)
                    {
                        EventProcessor.Events.Enqueue((@event, subscription, 1));
                        logger.LogDebug("Event published {Id} {EventType} published for {Subscription}", @event.Id, @event.EventType, subscription);
                    }
                }

                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                logger.LogWarning("{Method} : {Error}\n{JsonRootElement}", nameof(ReceiveAsync), ex.Message, json.RootElement);
                context.Response.StatusCode = 400;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("{Method} : {ExceptionType} : {Error}\n{StackTrace}", nameof(ReceiveAsync), ex.GetType().Name, ex.Message, ex.StackTrace);
            context.Response.StatusCode = 500;
        }
    }

    public EventReceiver(IOptions<Services> options, StorageClient storageClient, ILogger<EventReceiver> logger)
        => ctor = (options.Value.KeyByEventType(logger), storageClient, logger);
    readonly (EventTypeMap eventTypeMap, StorageClient storageClient, ILogger logger) ctor;
}
