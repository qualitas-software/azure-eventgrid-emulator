using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Qs.EventGrid.Emulator
{
    public class Program
    {
        public static void Main(string[] args)
            => CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(SetupWebHost);

        static void SetupWebHost(IWebHostBuilder builder)
            => builder.UseStartup<Startup>();
    }
}
