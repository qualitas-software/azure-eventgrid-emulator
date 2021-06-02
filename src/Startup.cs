using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Qs.EventGrid.Emulator
{
    using static Constants;

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
            => services.AddHostedService<EventProcessor>()
                       .AddSingleton<EventReceiver>()
                       .AddHttpClient<IEventGridClient, EventGridClient>();

        public void Configure(IApplicationBuilder app, IWebHostEnvironment _)
            => app.UseHttpsRedirection()
                  .UseRouting()
                  .UseEndpoints(ConfigureEndpoints);

        void ConfigureEndpoints(IEndpointRouteBuilder e)
        {
            e.MapGet("/", async ctx => await ctx.Response.WriteAsJsonAsync(new { App = Namespace }));
            e.MapPost("/api/events", e.ServiceProvider.GetRequiredService<EventReceiver>().ReceiveAsync);
        }
    }
}
