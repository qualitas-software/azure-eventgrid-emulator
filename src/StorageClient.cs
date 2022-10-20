using Azure.Messaging.EventGrid;
using Azure.Storage.Queues;

namespace Qs.EventGrid.Emulator;

public enum QueueTypes { Received, Deadletter }

public class StorageClient
{
    internal Task<string> EnqueueReceivedEventAsync(EventGridEvent @event, params Subscription[] subscriptions)
        => EnqueueMessageAsync(new { @event, subscriptions = subscriptions?.Select(s => $"{s}") }, QueueTypes.Received);

    /// <summary>Write <paramref name="event"/> to deadletter queue for <paramref name="subscription"/>.  Suitable for use in <see langword="catch"/> blocks when <paramref name="safeLogger"/> supplied.</summary>
    /// <param name="safeLogger">If null, any error is thrown.</param>
    internal async Task<string> EnqueueDeadletteredEventAsync(EventGridEvent @event, string deadletterReason, Subscription subscription, int attempt, DateTime receivedUtc, ILogger safeLogger)
    {
        try
        {
            return await EnqueueMessageAsync(new { @event, deadletterReason, subscription = $"{subscription}", attempt, receivedUtc }, QueueTypes.Deadletter);
        }
        catch (Exception ex)
        {
            if (safeLogger == null) throw;
            safeLogger.LogWarning(ex, "Unable to deadletter event {EventId}", @event.Id);
            return null;
        }
    }

    public async Task<string> EnqueueMessageAsync<T>(T t, QueueTypes queue) where T : class
    {
        var queueClient = queues[queue];
        if (queueClient == null) return null;

        var response = await queueClient.SendMessageAsync(t.ToJson(true));
        return response?.Value?.MessageId;
    }

    const string ConnectionString = "UseDevelopmentStorage=true";

    public StorageClient(IConfiguration config, ILogger<StorageClient> logger)
    {
        if (!config.GetValue<bool>("Storage:Enabled")) return;

        var queues = new Dictionary<QueueTypes, QueueClient>();

        foreach (var queueTypeValue in Enum.GetValues<QueueTypes>())
        {
            var queueTypeText = Enum.GetName(queueTypeValue);
            try
            {
                var queueName = config[$"Storage:{queueTypeText}Queue"] ?? $"qs-aeg-emulator-{queueTypeText.ToLower()}";
                var queueClient = new QueueClient(ConnectionString, queueName);
                var respose = queueClient.CreateIfNotExists();
                queues.Add(queueTypeValue, queueClient);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Local storage support enabled but unavailable.  (Queue: {Queue})", queueTypeText);
            }
        }
        this.queues = queues;
    }
    readonly IReadOnlyDictionary<QueueTypes, QueueClient> queues;
}
