using CliFx;
using EmbeddedPostgres.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace EmbeddedPostgres.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddedPostgresCliServices(this IServiceCollection services)
    {
        services.AddCliCommandsFromThisAssembly();
        return services;
    }

    public static void AddCliCommandsFromThisAssembly(this IServiceCollection services)
    {
        // Assuming you want to register commands from this assembly
        foreach (var commandType in typeof(Program).Assembly.GetTypes())
        {
            if (commandType.IsClass && !commandType.IsAbstract && typeof(ICommand).IsAssignableFrom(commandType))
            {
                services.AddTransient(commandType);
            }
        }
    }
}
