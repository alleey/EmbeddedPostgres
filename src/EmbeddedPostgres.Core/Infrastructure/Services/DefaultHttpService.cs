using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

/// <summary>
/// Provides HTTP-related functionalities for downloading artifacts to a specified target directory.
/// </summary>
internal class DefaultHttpService : IHttpService
{
    /// <summary>
    /// Extension given to the in-progress download before it is published to its final name.
    /// </summary>
    private const string PartialSuffix = ".partial";

    private readonly HttpClient httpClient;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<DefaultHttpService> logger;

    public DefaultHttpService(HttpClient httpClient, IFileSystem fileSystem, ILogger<DefaultHttpService> logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Downloads a collection of artifacts to the specified target directory asynchronously.
    /// </summary>
    /// <param name="sourceUrl">URL to be downloaded.</param>
    /// <param name="destDirectory">The directory where downloaded artifacts will be saved.</param>
    /// <param name="destFilename">The name of downloaded resource. If no name is provided the name is constructed from the URL. The constructed name is 
    /// guaranteed to be valid and remains the same for the same sourceUrl.</param>
    /// <param name="force">Specifies whether to overwrite existing files in the target directory. The default value is <c>false</c>.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the download to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous download operation. The task result contains a collection
    /// of file paths corresponding to the successfully downloaded artifacts.
    /// </returns>
    public async Task<string> DownloadAsync(
        string sourceUrl,
        string destDirectory,
        string destFilename = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = string.IsNullOrEmpty(destFilename) ? fileSystem.ConvertToValidFilename(Path.GetFileName(sourceUrl)) : destFilename;
        var targetFilename = Path.Combine(destDirectory, fileName);

        fileSystem.EnsureDirectory(destDirectory);
        fileSystem.RequireNotDirectory(targetFilename);

        // If the force flag is set and item already exists, remove it
        if (force)
        {
            if (fileSystem.FileOrDirectoryExists(targetFilename))
            {
                logger.LogInformation($"{targetFilename} already exist. Force deleting it.");

                // Caution: this will delete a folder (if it exists with the same name)
                fileSystem.DeleteFile(targetFilename);
            }
        }

        // A previous run that failed part way through can leave an empty file behind. Treat that as a
        // cache miss, otherwise the truncated file is served as though it were a complete download.
        if (fileSystem.FileExists(targetFilename) && fileSystem.GetFileEntryInfo(targetFilename).Size == 0)
        {
            logger.LogWarning($"{targetFilename} exists but is empty. Discarding it and downloading again.");
            fileSystem.DeleteFile(targetFilename);
        }

        // Download if needed
        if (!fileSystem.FileExists(targetFilename))
        {
            logger.LogInformation($"Downloading {sourceUrl} to {targetFilename}");

            // Download into a sibling temporary file and publish it only once the transfer has
            // completed, so a failed or cancelled download never leaves a partial file in the cache.
            var partialFilename = targetFilename + PartialSuffix;
            fileSystem.DeleteFile(partialFilename);

            try
            {
                using (var outputFile = fileSystem.Open(partialFilename, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true))
                {
                    await httpClient.DownloadAsync(sourceUrl, outputFile, cancellationToken).ConfigureAwait(false); // Let the exception bubble
                }

                fileSystem.MoveFile(partialFilename, targetFilename, overwrite: true);
            }
            catch
            {
                fileSystem.DeleteFile(partialFilename);
                throw;
            }
        }

        return targetFilename;
    }
}
