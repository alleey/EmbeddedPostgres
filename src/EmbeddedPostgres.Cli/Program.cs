using CliFx;
using EmbeddedPostgres.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmbeddedPostgres.Cli;

class Program
{
    public static async Task<int> Main()
    {
        var builder = new HostBuilder();

        // Configure services
        builder
            .ConfigureLogging(logging => logging.AddConsole())
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureServices(services);
            });

        var host = builder.Build();

        // Run the CLI application logic
        var cliApp = new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeActivator(commandTypes => host.Services)
            .Build();

        // Run the application
        await cliApp.RunAsync(["test"]);
        return 0;
    }

    // Configure DI services
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddEmbeddedPostgresCoreServices();
        services.AddEmbeddedPostgresServices();
        services.AddEmbeddedPostgresCliServices();
    }
}