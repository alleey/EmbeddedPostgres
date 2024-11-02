using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;
using EmbeddedPostgres.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;

namespace EmbeddedPostgres;

public class PgServerBuilder
{
    private readonly IPgInstanceBuilder instanceBuilder;
    private readonly IPgEnvironmentBuilder bootstrapper;

    public PgServerBuilder(IPgInstanceBuilder instanceBuilder, IPgEnvironmentBuilder bootstrapper)
    {
        this.instanceBuilder = instanceBuilder;
        this.bootstrapper = bootstrapper;
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
            await bootstrapper.ValidateAsync(instanceOptions.InstanceDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
            hasEnvironment = true;
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
                await DestoryExistingAsync(options, cancellationToken).ConfigureAwait(false);
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
            await bootstrapper.ValidateAsync(instanceOptions.InstanceDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var environment = await bootstrapper.BuildAsync(instanceOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var cluster in options.DataClusters)
        {
            environment.DataClusters.Add(cluster.Configuration);
        }

        // Pass builder options in the Extras dictionary

        return environment;
    }

    /// <summary>
    /// Asynchronously destroys the specified PostgreSQL server instance by stopping it 
    /// and then cleaning up its resources.
    /// </summary>
    /// <param name="server">The <see cref="PgServer"/> instance to be destroyed.</param>
    /// <param name="shutdownParams">The parameters for shutting down the PostgreSQL server.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Default is <c>CancellationToken.None</c>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="Exception">Thrown if there is an error while stopping or destroying the server.</exception>
    public async Task DestroyAsync(
        PgServer server,
        PgShutdownParams shutdownParams,
        IEnumerable<string> dataDirectories = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        cancellationToken.ThrowIfCancellationRequested();

        await server.DataClusters.ParallelForEachAsync(
            async cluster =>
            {
                await cluster.DestroyAsync(shutdownParams, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, 
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await instanceBuilder.DestroyAsync(server.Environment.Instance, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task DestoryExistingAsync(PgServerBuilderOptions builderOptions, CancellationToken cancellationToken = default)
    {
        // Make copy of options without any params, we dont want to execute any fixes etc.
        var instanceOptions = builderOptions.InstanceOptions with
        {
            PlatformParameters = new Dictionary<string, object>()
        };

        var makeShiftEnv = await bootstrapper.BuildAsync(instanceOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var cluster in builderOptions.DataClusters)
        {
            makeShiftEnv.DataClusters.Add(cluster.Configuration);
        }

        // CreateTargetDatabase a temp serer so we can issue a stop command
        var makeShiftServer = new PgServer(makeShiftEnv);

        // Now stop the server and destroy instance
        await DestroyAsync(makeShiftServer, PgShutdownParams.Fast, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
