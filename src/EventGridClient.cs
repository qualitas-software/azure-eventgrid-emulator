using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using static System.Text.Encoding;

namespace Qs.EventGrid.Emulator
{
    using static Constants;

    class EventGridClient : IEventGridClient
    {
        public async Task SendEventAsync(string subscriberFunctionName, params EventGridEvent[] events)
        {
            var content = new StringContent(events.ToJson(), UTF8, JsonMediaType);

            var response = await httpClient.PostAsync($@"{webhook}/{eventSubscriber}={subscriberFunctionName}", content);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Invalid http response: {response.StatusCode}");
        }

        public EventGridClient(HttpClient httpClient, IConfiguration config)
        {
            httpClient.BaseAddress = new Uri($"http://localhost:{Port}/");
            httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
            this.httpClient = httpClient;
        }
        readonly HttpClient httpClient;

        const string JsonMediaType = "application/json";
        const string webhook = @"runtime/webhooks";
        const string eventSubscriber = @"eventgrid?functionName";
    }
}
