﻿using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

internal class DefaultPgArtifactsBuilder  : IPgArtifactsBuilder
{
    private readonly HttpClient httpClient;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<DefaultPgArtifactsBuilder> logger;

    public DefaultPgArtifactsBuilder(
                HttpClient httpClient,
                IFileSystem fileSystem, ILogger<DefaultPgArtifactsBuilder> logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.logger = logger;
    }

    /// <summary>
    /// Downloads the specified artifacts if they are not already present.
    /// Note that these artifacts are downloaded to the <see cref="CacheDirectory"/>, 
    /// which may be different from the instance directory.
    /// </summary>
    /// <param name="artifacts">
    /// A collection of artifacts to download. Each artifact should specify its 
    /// source and any other necessary metadata required for the download process.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task will complete 
    /// with a collection of downloaded artifacts, including their updated metadata 
    /// after the download.
    /// </returns>
    public async Task<IEnumerable<PgArtifact>> BuildAsync(IEnumerable<PgArtifact> artifacts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLocalArtifacts(artifacts);

        var downloaded = new ConcurrentBag<PgArtifact>();

        // Lets do this in parallel
        await artifacts.ParallelForEachAsync(async item =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceUri = item.Source;
            var fileName = fileSystem.ConvertToValidFilename(Path.GetFileName(sourceUri));
            var targetFilename = Path.Combine(item.Target, fileName);

            fileSystem.EnsureDirectory(item.Target);

            if (!item.IsLocal)
            {
                // If the force flag is set and item already exists, remove it
                if (fileSystem.FileOrDirectoryExists(targetFilename))
                {
                    if (item.Force)
                    {
                        fileSystem.RequireNotDirectory(targetFilename);

                        logger.LogInformation($"{targetFilename} already exist. Force deleting it.");

                        // Caution: this will delete a folder (if it exists with the same name)
                        fileSystem.DeleteFile(targetFilename);
                    }
                }

                // Download if needed
                if (!fileSystem.FileExists(targetFilename))
                {
                    logger.LogInformation($"Downloading {sourceUri} to {targetFilename}");

                    using var outputFile = fileSystem.Open(targetFilename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 64 * 1024, true);

                    await httpClient.DownloadAsync(sourceUri, outputFile, cancellationToken).ConfigureAwait(false); // Let the exception bubble
                }

                downloaded.Add(item with { Source = targetFilename, IsLocal = true });
            }
            else
            {
                downloaded.Add(item);
            }

        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        return downloaded.ToArray();
    }

    /// <summary>
    /// Validate all local artifacts
    /// </summary>
    /// <param name="artifacts"></param>
    /// <exception cref="PgValidationException"></exception>
    private void ValidateLocalArtifacts(IEnumerable<PgArtifact> artifacts)
    {
        logger.LogInformation($"Validating artifacts");

        if (!artifacts.Any(item => item.Kind == PgArtifactKind.Main))
        {
            throw new PgValidationException($"Main set of binaries for Postgres wan't specified");
        }

        var extensions = artifacts.Where(item => item.Kind != PgArtifactKind.Main);
        foreach (var artifact in artifacts)
        {
            if (artifact.IsLocal)
            {
                if (!fileSystem.FileExists(artifact.Source))
                {
                    throw new PgValidationException($"The artifact file {artifact.Source} does not exist");
                }
            }
        }
    }
}