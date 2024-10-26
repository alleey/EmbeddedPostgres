using EmbeddedPostgres.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace EmbeddedPostgres.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddedPostgresServices(this IServiceCollection services)
    {
        services.AddSingleton<PgServerBuilder>();
        return services;
    }
}
