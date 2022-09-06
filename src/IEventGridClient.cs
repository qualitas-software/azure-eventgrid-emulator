using Azure.Messaging.EventGrid;

namespace Qs.EventGrid.Emulator;

public interface IEventGridClient
{
    Task<string> SendEventAsync(string baseAddress, Endpoint endpoint, params EventGridEvent[] events);
}
