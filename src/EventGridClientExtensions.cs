namespace Qs.EventGrid.Emulator;

static class EventGridClientExtensions
{
    internal static void AddEventGridClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IEventGridClient, EventGridClient>();

        var subs = configuration.GetSection(typeof(Services).Name).Get<Services>();
        subs.ForEach(sub =>
        {
            services.AddHttpClient(sub.BaseAddress, httpClient =>
            {
                httpClient.BaseAddress = new Uri(sub.BaseAddress.EnsureTrailing());
                httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
            });
        });
    }
}
