using Azure.Messaging.EventGrid;
using static System.Text.Encoding;

namespace Qs.EventGrid.Emulator;

using static Constants;

class EventGridClient : IEventGridClient
{
    async Task<string> IEventGridClient.SendEventAsync(string baseAddress, Endpoint endpoint, params EventGridEvent[] events)
    {
        var content = new StringContent(events.ToJson(), UTF8, JsonMediaType);
        var path = endpoint.EventGridFunction == null ? endpoint.Path : $"{EventGridSubscriber}={endpoint.EventGridFunction}";
        var httpClient = httpClientFactory.CreateClient(baseAddress);

        var response = await httpClient.PostAsync(path, content);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Invalid http response: {response.StatusCode}");

        return endpoint.EventGridFunction ?? endpoint.Path;
    }

    public EventGridClient(IHttpClientFactory httpClientFactory)
        => this.httpClientFactory = httpClientFactory;
    readonly IHttpClientFactory httpClientFactory;
}
