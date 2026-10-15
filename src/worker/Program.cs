using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MedioNet.Worker
{
    public class Program
    {
        // Friendly command-line switches mapped onto the Medio config section,
        // e.g. `--source D:\photos` overrides `Medio:SourcePath`.
        private static readonly Dictionary<string, string> SwitchMappings = new()
        {
            ["--source"] = "Medio:SourcePath",
            ["-s"] = "Medio:SourcePath",
        };

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(config =>
                {
                    // Added last so it wins over appsettings*.json and env vars.
                    config.AddCommandLine(args, SwitchMappings);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<MedioOptions>(hostContext.Configuration.GetSection("Medio"));
                    services.AddHostedService<Worker>();
                    services.AddTransient<IMediaOrganizer, MediaOrganizer>();
                });
    }
}
