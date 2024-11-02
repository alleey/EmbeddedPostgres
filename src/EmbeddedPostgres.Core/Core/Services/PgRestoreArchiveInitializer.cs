using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Extensions;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

/// <summary>
/// Represents the initialization source for PostgreSQL data clusters by restoring from an archive.
/// This class handles the process of restoring a PostgreSQL data cluster from an archive file,
/// applying the configuration provided in the options.
/// Implements the <see cref="IPgClusterInitializer"/> interface.
/// </summary>
/// <param name="environment">Provides access to the environment configuration and operations for PostgreSQL clusters.</param>
/// <param name="options">Specifies options for restoring the data cluster from an archive, such as the archive file path and forced reinitialization.</param>
internal class PgRestoreArchiveInitializer(PgEnvironment environment, PgRestoreArchiveOptions options) : IPgClusterInitializer
{
    /// <summary>
    /// Asynchronously initializes a PostgreSQL data cluster by restoring data from a specified archive file.
    /// Ensures the cluster is stopped before restoration and optionally reinitializes if the cluster is already initialized.
    /// </summary>
    /// <param name="dataCluster">The configuration object representing the PostgreSQL data cluster to initialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="PgCoreException">
    /// Thrown if the data cluster is not in the stopped state during restoration.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the archive file specified in the options does not exist.
    /// </exception>
    public async Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Ensure the archive file exists
        environment.FileSystem.RequireFile(options.ArchiveFilePath);

        // Get the status of the data cluster to ensure it is in the stopped state
        var status = await environment.DataClusterController.GetStatusAsync(dataCluster, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (status.IsValid)
        {
            // Throw an exception if the data cluster is not stopped
            throw new PgCoreException($"Data cluster {dataCluster.UniqueId} must be in the stopped state for initialization.");
        }

        // Get the full path to the data directory for the data cluster
        var dataDirectory = environment.Instance.GetDataFullPath(dataCluster);

        // Check if the data cluster has already been initialized
        if (environment.InitDbController.IsInitialized(dataCluster))
        {
            // If the force reinitialization option is enabled, delete the existing data directory
            if (options.ForceReInitialization)
            {
                environment.FileSystem.DeleteDirectory(dataDirectory);
            }
            else
            {
                // If reinitialization is not forced, exit the method without further action
                return;
            }
        }

        // Extract the archive file into the data directory using the default extraction strategy
        var fileExtractor = environment.FileExtractorFactory.ForExtractionStrategy(KnownExtractionStrategies.Default);
        await fileExtractor.ExtractAsync(options.ArchiveFilePath, dataDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
