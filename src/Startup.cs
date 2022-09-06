namespace Qs.EventGrid.Emulator;

using static Constants;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
        => services.AddHostedService<EventProcessor>()
                   .AddSingleton<EventReceiver>()
                   .AddOptions<Services>()
                   .AddEventGridClients(configuration);

    public void Configure(IApplicationBuilder app, IWebHostEnvironment _)
        => app.UseHttpsRedirection()
              .UseRouting()
              .UseEndpoints(ConfigureEndpoints);

    void ConfigureEndpoints(IEndpointRouteBuilder e)
    {
        e.MapGet("/", async ctx => await ctx.Response.WriteAsJsonAsync(new { App = Namespace }));
        var b = e.MapPost(EventGridReceiverPath, e.ServiceProvider.GetRequiredService<EventReceiver>().ReceiveAsync);

        var endpoint = $"{configuration["Kestrel:EndPoints:Https:Url"]?.EnsureTrailing("/")}{EventGridReceiverPath.TrimStart('/')}";
        e.ServiceProvider.GetLogger<Startup>().LogInformation("Post events to {EventEndpoint}", endpoint);
    }

    public Startup(IConfiguration configuration) => this.configuration = configuration;
    readonly IConfiguration configuration;
}