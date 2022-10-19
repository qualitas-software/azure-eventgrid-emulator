using Azure.Messaging.EventGrid;
using Azure.Storage.Queues;

namespace Qs.EventGrid.Emulator;

public enum Queues { Received }

public class StorageClient
{
    internal Task<string> EnqueueReceivedEventAsync(EventGridEvent @event, params Subscription[] subscriptions)
        => EnqueueMessageAsync(new { @event, subscriptions = subscriptions?.Select(s => $"{s}") }, Queues.Received);

    public async Task<string> EnqueueMessageAsync<T>(T t, Queues queue) where T : class
    {
        var queueClient = GetQueueClient(queue);
        if (queueClient == null) return null;

        var response = await queueClient.SendMessageAsync(t.ToJson(true));
        return response?.Value?.MessageId;
    }

    QueueClient GetQueueClient(Queues queue) => queue switch
    {
        Queues.Received => receivedQueue,
        _ => throw new NotSupportedException($"{queue} not supported.")
    };

    public StorageClient(ILogger<StorageClient> logger)
    {
        try
        {
            receivedQueue = new("UseDevelopmentStorage=true", "qs-aeg-emulator-received");
            var respose = receivedQueue.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Local storage support unavailable.");
        }
    }
    readonly QueueClient receivedQueue;
}
