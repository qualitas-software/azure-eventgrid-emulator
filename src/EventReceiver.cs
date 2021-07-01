using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Qs.EventGrid.Emulator
{
    using static Constants;
    using static JsonHelpers;

    class EventReceiver
    {
        public async Task ReceiveAsync(HttpContext context)
        {
            context.Response.Headers.Add("App", Namespace);

            try
            {
                var json = await context.Request.ReadFromJsonAsync<JsonDocument>(JsonSerializerOptions);

                try
                {
                    var @events = json.FromJson<List<EventGridEvent>>();
                    foreach (var @event in events)
                    {
                        EventProcessor.Queue.Enqueue((@event, 1));
                        logger.LogDebug($"Event published {@event.Id} {@event.EventType} {@event.Subject}");
                    }

                    context.Response.StatusCode = 200;
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"{nameof(ReceiveAsync)} : {ex.Message}\n{json.RootElement}");
                    context.Response.StatusCode = 400;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{nameof(ReceiveAsync)} : {ex.GetType().Name} : {ex.Message}\n{ex.StackTrace}");
                context.Response.StatusCode = 500;
            }
        }

        public EventReceiver(ILogger<EventReceiver> logger) => this.logger = logger;
        readonly ILogger logger;
    }
}
