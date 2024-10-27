using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Core.Services;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using EmbeddedPostgres.Infrastructure.Services;
using EmbeddedPostgres.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Runtime.InteropServices;

namespace EmbeddedPostgres.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const int DefaultHttpRequestTimeoutSecs = 10 * 60; // Inclusive of retries
    private const int DefaultHttpRequestRetries = 5;

    public static IServiceCollection AddEmbeddedPostgresCoreServices(this IServiceCollection services)
    {
        AddInfrastructureServices(services);

        services.AddSingleton<IPgArtifactsBuilder, DefaultPgArtifactsBuilder>();
        services.AddSingleton<IPgInstanceBuilder, DefaultPgInstanceBuilder>();

        // Register factory methods for Controllers
        AddControllerFactories(services);
        AddEnvironmentBuilder(services);

        return services;
    }

    private static void AddInfrastructureServices(IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var configuration = sp.GetRequiredService<IConfiguration>();

        var httpTimeoutSecs = configuration.GetValue("Http:RequestTimeout", DefaultHttpRequestTimeoutSecs);
        var httpRetries = configuration.GetValue("Http:MaxRetries", DefaultHttpRequestRetries);

        services.AddHttpClient<PgInstallationSource>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(httpTimeoutSecs);
        })
        .AddPolicyHandler(HttpRetryPolicyBuilder.Retry(httpRetries)
                                                .HandleTimeout(httpTimeoutSecs)
                                                .HandleHttpStatus(HttpStatusCode.NotFound)
                                                .Build());

        services.AddSingleton<IFileSystem, DefaultFileSystem>();
        services.AddSingleton<ICommandExecutor, DefaultCommandExecutor>();

        // Register the extractors
        services.AddKeyedSingleton<IFileExtractor, SystemFileExtractor>(KnownExtractionStrategies.System);
        services.AddKeyedSingleton<IFileExtractor, SharpFileExtractor>(KnownExtractionStrategies.Sharp);
        services.AddKeyedSingleton<IFileExtractor, ZonkyFileExtractor>(KnownExtractionStrategies.Zonky);
        services.AddSingleton<IFileCompressor, SystemFileCompressor>();

        services.AddSingleton<IFileExtractorFactory, FileExtractorFactory>();
        // Injecting factory of factory to avoid circular dependency in extractors which depend on IFileExtractorFactory
        services.AddSingleton<Func<IFileExtractorFactory>>(ctx => () => ctx.GetRequiredService<IFileExtractorFactory>());
    }

    private static void AddEnvironmentBuilder(IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IPgEnvironmentBuilder, PgEnvironmentBuilderWindows>();
        }
        else
        {
            services.AddSingleton<IPgEnvironmentBuilder, PgEnvironmentBuilderLinux>();
        }
    }

    private static void AddControllerFactories(IServiceCollection services)
    {
        services.AddSingleton<Func<string, PgInstanceConfiguration, IPgDataClusterController>>(ctx =>
            (string pathOrFilename, PgInstanceConfiguration instance) =>
            {
                return new PgDataClusterController(
                    pathOrFilename,
                    instance,
                    ctx.GetRequiredService<IFileSystem>(),
                    ctx.GetRequiredService<ICommandExecutor>()
                );
            });

        services.AddSingleton<Func<string, PgInstanceConfiguration, IPgInitDbController>>(ctx =>
            (string pathOrFilename, PgInstanceConfiguration instance) =>
            {
                return new PgInitDbController(
                    pathOrFilename,
                    instance,
                    ctx.GetRequiredService<IFileSystem>(),
                    ctx.GetRequiredService<ICommandExecutor>()
                );
            });

        services.AddSingleton<Func<string, PgInstanceConfiguration, IPgSqlController>>(ctx =>
            (string pathOrFilename, PgInstanceConfiguration instance) =>
            {
                return new PgSqlController(
                    pathOrFilename,
                    instance,
                    ctx.GetRequiredService<IFileSystem>(),
                    ctx.GetRequiredService<ICommandExecutor>()
                );
            });
    }
}