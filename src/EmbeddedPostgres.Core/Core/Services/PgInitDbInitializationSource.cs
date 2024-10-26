using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

/// <summary>
/// Represents the initialization source for PostgreSQL data clusters using the InitDb process.
/// This class handles the process of initializing or reinitializing the PostgreSQL data cluster
/// by interacting with the PgEnvironment and applying the configuration provided in the options.
/// Implements the <see cref="IPgInitializationSource"/> interface.
/// </summary>
/// <param name="environment">Provides access to the environment configuration and operations for PostgreSQL clusters.</param>
/// <param name="options">Specifies options for controlling the initialization behavior, such as forced reinitialization.</param>
internal class PgInitDbInitializationSource(PgEnvironment environment, PgInitDbOptions options) : IPgInitializationSource
{
    /// <summary>
    /// Asynchronously initializes a PostgreSQL data cluster based on the given configuration.
    /// Ensures the cluster is stopped before initialization and optionally reinitializes if already initialized.
    /// </summary>
    /// <param name="dataCluster">The configuration object representing the PostgreSQL data cluster to initialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="PgCoreException">
    /// Thrown if the data cluster is not in the stopped state during initialization.
    /// </exception>
    public async Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default)
    {
        // Get the status of the data cluster to ensure it is in the stopped state
        var status = await environment.Controller.GetStatusAsync(dataCluster, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (status.IsValid)
        {
            // Throw an exception if the data cluster is not stopped
            throw new PgCoreException($"Data cluster {dataCluster.UniqueId} must be in the stopped state for initialization.");
        }

        // Check if the data cluster has already been initialized
        if (environment.InitDb.IsInitialized(dataCluster))
        {
            // If the force reinitialization option is enabled, delete the existing data directory
            if (options.ForceReInitialization)
            {
                environment.FileSystem.DeleteDirectory(environment.Instance.GetDataFullPath(dataCluster));
            }
            else
            {
                // If reinitialization is not forced, exit the method without further action
                return;
            }
        }

        // Perform the initialization asynchronously
        await environment.InitDb.InitializeAsync(dataCluster, cancellationToken).ConfigureAwait(false);
    }
}
