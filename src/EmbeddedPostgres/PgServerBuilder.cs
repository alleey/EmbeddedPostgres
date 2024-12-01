using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;

namespace EmbeddedPostgres;

public class PgServerBuilder
{
    private readonly IPgInstanceBuilder instanceBuilder;
    private readonly IPgEnvironmentBuilder environmentBuilder;

    public PgServerBuilder(IPgInstanceBuilder instanceBuilder, IPgEnvironmentBuilder environmentBuilder)
    {
        this.instanceBuilder = instanceBuilder ?? throw new ArgumentNullException(nameof(instanceBuilder));
        this.environmentBuilder = environmentBuilder ?? throw new ArgumentNullException(nameof(environmentBuilder));
    }

    /// <summary>
    /// Asynchronously builds a PostgreSQL environment based on the provided configuration options.
    /// </summary>
    /// <param name="builder">An action to configure the <see cref="PgServerBuilderOptions"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Default is <c>CancellationToken.None</c>.</param>
    /// <returns>A task that represents the asynchronous operation, containing the built <see cref="PgEnvironment"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="PgCoreException">Thrown when validation of the existing environment fails.</exception>
    /// <exception cref="Exception">Thrown if an error occurs during the building or validation process.</exception>
    public Task<PgEnvironment> BuildAsync(Action<PgServerBuilderOptions> builder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(builder);

        var builderOptions = new PgServerBuilderOptions();
        builder(builderOptions);

        return BuildAsync(builderOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously builds a PostgreSQL environment based on the provided configuration options.
    /// </summary>
    /// <param name="options">A configured instance of <see cref="PgServerBuilderOptions"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Default is <c>CancellationToken.None</c>.</param>
    /// <returns>A task that represents the asynchronous operation, containing the built <see cref="PgEnvironment"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="PgCoreException">Thrown when validation of the existing environment fails.</exception>
    /// <exception cref="Exception">Thrown if an error occurs during the building or validation process.</exception>
    public async Task<PgEnvironment> BuildAsync(PgServerBuilderOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);

        options.Build();
        options.Validate();

        var instanceOptions = options.InstanceOptions;
        bool buildEnvironment = instanceOptions.CleanInstall;
        bool hasEnvironment = false;

        try
        {
            // If clean install isn't required, check if we have a working environment
            // This will save a lot of time and cpu
            //
            var binaries = await environmentBuilder.ValidateAsync(instanceOptions.InstanceDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
            hasEnvironment = binaries.Count == 3;
        }
        catch (PgCoreException)
        {
            // Validation failed, lets build
            buildEnvironment = true;
        }

        if (buildEnvironment)
        {
            if (hasEnvironment)
            {
                await DestroyAsync(options, PgShutdownParams.Fast, cancellationToken).ConfigureAwait(false);
            }

            var installationSource = new PgInstallationSource(options.CacheDirectory);
            installationSource.UseMain(options.ServerArtifact);
            foreach (var ext in options.extensions)
            {
                installationSource.UseExtension(ext);
            }

            // Download and extract the binaries
            await instanceBuilder.BuildAsync(instanceOptions, installationSource.Build(), cancellationToken: cancellationToken).ConfigureAwait(false);

            // Lets see if we have a working environment
            await environmentBuilder.ValidateAsync(instanceOptions.InstanceDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var environment = await environmentBuilder.BuildAsync(instanceOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var cluster in options.DataClusters)
        {
            environment.DataClusters.Add(cluster.Configuration);
        }

        // Pass builder options in the Extras dictionary

        return environment;
    }

    /// <summary>
    /// Asynchronously destroys the specified PostgreSQL server instance by stopping it 
    /// and then cleaning up its resources. This includes shutting down all data clusters 
    /// and removing any associated instance data.
    /// </summary>
    /// <param name="server">
    /// The <see cref="PgServer"/> instance to be destroyed. This represents the PostgreSQL 
    /// server whose resources will be cleaned up.
    /// </param>
    /// <param name="shutdownParams">
    /// The parameters for shutting down the PostgreSQL server, which may specify shutdown 
    /// modes or additional flags.
    /// </param>
    /// <param name="dataDirectories">
    /// Optional. A collection of data directory paths that should be considered for cleanup 
    /// after stopping the server. If null, defaults to the server's data clusters.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing the operation to be canceled 
    /// if necessary. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous destroy operation. The task completes once the 
    /// server has been stopped and all resources have been successfully cleaned up.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    /// <exception cref="Exception">
    /// Thrown if there is an error while stopping or destroying the server, such as failure 
    /// to shut down data clusters or clean up resources.
    /// </exception>
    public async Task DestroyAsync(
        PgServer server,
        PgShutdownParams shutdownParams,
        IEnumerable<string> dataDirectories = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        cancellationToken.ThrowIfCancellationRequested();

        // Stop all data clusters in the server using the specified shutdown parameters
        await server.StopAsync(
            PgServer.AllDataClusters,
            shutdownParams,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Destroy the instance and clean up resources associated with the environment
        await instanceBuilder.DestroyAsync(server.Environment.Instance, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Destroys the PostgreSQL server instance specified by the provided options, shutting it down
    /// cleanly and deleting any related data and resources.
    /// </summary>
    /// <param name="builderOptions">
    /// The options used to configure the PostgreSQL server instance. This includes configuration
    /// for data clusters and other instance-specific settings.
    /// </param>
    /// <param name="shutdownParams">
    /// Parameters for shutting down the server, which may include details such as the shutdown
    /// mode and any additional shutdown flags.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default value
    /// is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous destroy operation. The task will complete
    /// once the server instance has been shut down and resources have been cleaned up.
    /// </returns>
    public async Task DestroyAsync(
        PgServerBuilderOptions builderOptions,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken = default)
    {
        // Make a copy of instance options without platform parameters to avoid executing fixes or updates
        var instanceOptions = builderOptions.InstanceOptions with
        {
            PlatformParameters = new Dictionary<string, object>()
        };

        // Build a temporary environment based on instance options, without original platform parameters
        var makeShiftEnv = await environmentBuilder.BuildAsync(instanceOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Add configured data clusters to the temporary environment
        foreach (var cluster in builderOptions.DataClusters)
        {
            makeShiftEnv.DataClusters.Add(cluster.Configuration);
        }

        // Create a temporary server instance to issue a stop command
        var makeShiftServer = new PgServer(makeShiftEnv);

        // Stop the server and destroy the instance
        await DestroyAsync(makeShiftServer, shutdownParams, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
