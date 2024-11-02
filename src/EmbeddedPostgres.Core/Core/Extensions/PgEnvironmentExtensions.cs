using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Extensions;

public static class PgEnvironmentExtensions
{
    /// <summary>
    /// Returns the full path of the instance directory specified in the <paramref name="configuration"/>.
    /// </summary>
    /// <param name="environment">The <see cref="PgEnvironment"/> instance containing the directory details.</param>
    /// <returns>
    /// A string representing the full path to the instance directory.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetInstanceFullPath(this PgEnvironment environment)
        => environment.Instance.GetInstanceFullPath();

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and extracts its contents to a destination directory.
    /// </summary>
    /// <param name="environment">The PgEnvironment object providing access to services for HTTP download and file extraction.</param>
    /// <param name="sourceUrl">The URL of the file to download.</param>
    /// <param name="destDirectory">The directory where the extracted files will be placed.</param>
    /// <param name="cacheFilename">An optional name for the cached file. If null, a default name will be used.</param>
    /// <param name="cacheDirectory">An optional directory for caching the downloaded file. Defaults to ".cache" if not provided.</param>
    /// <param name="ignoreRootDir">If true, the root directory in the archive is ignored, extracting only subdirectories and files.</param>
    /// <param name="extractionStrategy">Allow overriding the extractor to use for extracting files.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    public static async Task DownloadExtractAsync(
        this PgEnvironment environment,
        string sourceUrl,
        string destDirectory,
        string cacheFilename = null,
        string cacheDirectory = null,
        bool ignoreRootDir = false,
        string extractionStrategy = null,
        CancellationToken cancellationToken = default)
    {
        // Check if the cancellation has been requested at the start of the operation
        cancellationToken.ThrowIfCancellationRequested();

        // Set a default cache directory if none is provided
        if (string.IsNullOrEmpty(cacheDirectory))
        {
            cacheDirectory = ".cache";
        }

        // Download the file asynchronously, using the cache directory and file name if specified
        var downloaded = await environment.HttpService.DownloadAsync(
            sourceUrl,
            cacheDirectory,
            cacheFilename,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Get an extractor based on the default extraction strategy
        var extractor = environment.FileExtractorFactory.ForExtractionStrategy(extractionStrategy ?? KnownExtractionStrategies.Default);

        // Extract the downloaded file to the specified destination directory
        await extractor.ExtractAsync(
            downloaded,
            destDirectory,
            ignoreRootDir: ignoreRootDir,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously destroys a PostgreSQL data cluster by stopping it and deleting its associated data directory.
    /// </summary>
    /// <param name="environment">The <see cref="PgEnvironment"/> instance that provides the context for the operation.</param>
    /// <param name="dataCluster">The configuration details of the PostgreSQL data cluster to be destroyed.</param>
    /// <param name="shutdownParams">
    /// The parameters for shutting down the PostgreSQL data cluster. If not provided, the default shutdown parameters will be used.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. It retrieves the status of the data cluster using <see cref="GetStatusAsync"/>.
    /// 2. If the data cluster is running (i.e., the status is valid), it stops the cluster using <see cref="StopAsync"/> with the provided shutdown parameters.
    /// 3. It then checks if the data directory associated with the cluster exists on the file system.
    /// 4. If the directory exists, it deletes the directory.
    /// 
    /// This operation is irreversible and will permanently delete all data associated with the specified PostgreSQL data cluster.
    /// Ensure that any necessary backups are made before calling this method.
    /// </remarks>
    public static async Task DestroyAsync(
        this PgEnvironment environment,
        PgDataClusterConfiguration dataCluster,
        PgShutdownParams shutdownParams = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataCluster);
        cancellationToken.ThrowIfCancellationRequested();

        var status = await environment.DataClusterController.GetStatusAsync(
            dataCluster,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (status.IsValid)
        {
            await environment.DataClusterController.StopAsync(
                dataCluster,
                shutdownParams ?? PgShutdownParams.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var dataDirectory = environment.Instance.GetDataFullPath(dataCluster);
        if (environment.FileSystem.DirectoryExists(dataDirectory))
        {
            environment.FileSystem.DeleteDirectory(dataDirectory);
        }
    }

    /// <summary>
    /// Asynchronously archives a PostgreSQL data cluster by stopping it and compressing its data directory into a specified archive file.
    /// </summary>
    /// <param name="environment">The <see cref="PgEnvironment"/> instance that provides the context for the operation.</param>
    /// <param name="dataCluster">The configuration details of the PostgreSQL data cluster to be archived.</param>
    /// <param name="archiveFilePath">
    /// The file path where the compressed archive of the data cluster will be saved. 
    /// This path must be a valid and writable location.
    /// </param>
    /// <param name="shutdownParams">
    /// The parameters for shutting down the PostgreSQL data cluster. If not provided, the default shutdown parameters will be used.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataCluster"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="archiveFilePath"/> is null or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. It stops the PostgreSQL data cluster using <see cref="StopAsync"/> with the provided shutdown parameters.
    /// 2. It retrieves the full path to the data directory associated with the specified cluster.
    /// 3. It compresses the data directory into the specified archive file using the <see cref="FileCompressor"/>.
    /// 
    /// Ensure that the archive file path provided is valid and accessible, as this method will overwrite existing files
    /// at that location without confirmation.
    /// </remarks>
    public static async Task ArchiveAsync(
        this PgEnvironment environment,
        PgDataClusterConfiguration dataCluster,
        string archiveFilePath,
        PgShutdownParams shutdownParams = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataCluster);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        var status = await environment.DataClusterController.GetStatusAsync(
            dataCluster,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (status.IsValid)
        {
            await environment.DataClusterController.StopAsync(
                dataCluster,
                shutdownParams ?? PgShutdownParams.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var dataDirectory = environment.Instance?.GetDataFullPath(dataCluster);

        await environment.FileCompressor.CompressAsync(
            dataDirectory,
            archiveFilePath,
            includeRootDir: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}