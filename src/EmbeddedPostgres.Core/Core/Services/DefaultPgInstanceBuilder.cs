using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

internal class DefaultPgInstanceBuilder : IPgInstanceBuilder
{
    private readonly IPgArtifactsBuilder artifactsBuilder;
    private readonly IFileSystem fileSystem;
    private readonly IFileExtractorFactory extractorFactory;
    private readonly ILogger<DefaultPgInstanceBuilder> logger;

    public DefaultPgInstanceBuilder(
        IPgArtifactsBuilder artifactsBuilder,
        IFileSystem fileSystem,
        IFileExtractorFactory extractorFactory,
        ILogger<DefaultPgInstanceBuilder> logger)
    {
        this.artifactsBuilder = artifactsBuilder;
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.extractorFactory = extractorFactory ?? throw new ArgumentNullException(nameof(extractorFactory));
        this.logger = logger;
    }

    /// <summary>
    /// Downloads and extracts the specified artifacts into the instance directory.
    /// </summary>
    /// <param name="options">
    /// The options for configuring the instance build process, including settings 
    /// related to the download and extraction of artifacts.
    /// </param>
    /// <param name="artifacts">
    /// A collection of artifacts to be downloaded and extracted for the PostgreSQL instance.
    /// These artifacts may include binaries, extensions, and other necessary files.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous build operation. The task will complete
    /// once the download and extraction of the artifacts have finished.
    /// </returns>
    public async Task BuildAsync(
        PgInstanceBuilderOptions options,
        IEnumerable<PgArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InstanceDirectory, nameof(options.InstanceDirectory));
        cancellationToken.ThrowIfCancellationRequested();

        var downloaded = await artifactsBuilder.BuildAsync(artifacts, cancellationToken).ConfigureAwait(false);
        
        // Make sure the folder is there, cleaning it if required
        if (options.CleanInstall)
        {
            logger.LogInformation($"Clean install is set. Deleting {options.InstanceDirectory}");
            await DestroyAsync(options, cancellationToken).ConfigureAwait(false);
        }

        await ExtractArtifactsAsync(
            options.InstanceDirectory,
            options.ExcludePgAdminInstallation,
            downloaded,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation($"Installation source build complete");
    }

    /// <summary>
    /// Destroys the contents of the specified instance directory, effectively 
    /// cleaning up all files and configurations associated with the PostgreSQL instance.
    /// </summary>
    /// <param name="options">
    /// The configuration settings for the PostgreSQL instance that should be destroyed.
    /// This includes paths and other relevant settings for the cleanup process.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous destroy operation. The task will complete
    /// once the instance directory has been cleaned up.
    /// </returns>
    public Task DestroyAsync(PgInstanceConfiguration options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (fileSystem.DirectoryExists(options.InstanceDirectory))
        {
            fileSystem.DeleteDirectory(options.InstanceDirectory);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// ExtractAsync binaries into <paramref name="instanceDirectory"/>
    /// </summary>
    /// <param name="instanceDirectory"></param>
    /// <param name="downloaded"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ExtractArtifactsAsync(
        string instanceDirectory,
        bool excludePgAdmin,
        IEnumerable<PgArtifact> downloaded,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pgServer = downloaded.Where(item => item.Kind == PgArtifactKind.Main).First();
        var extractor = extractorFactory.ForExtractionStrategy(pgServer.ExtractionStrategy);

        fileSystem.EnsureDirectory(instanceDirectory);

        // Excluding pgAdmin extraction can save ~650Mb of disk space
        Func<ArchiveEntry, bool> excludePgAdminPredicate = item => item.Key.StartsWith("pgsql/pgAdmin");
        await extractor.ExtractAsync(
            pgServer.Source,
            instanceDirectory,
            excludePredicate: excludePgAdmin ? excludePgAdminPredicate : null,
            ignoreRootDir: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation($"Extracted {pgServer.Source} into {instanceDirectory}");

        // Install all extensions, lets do that in parallel
        var extensions = downloaded.Where(item => item.Kind != PgArtifactKind.Main);
        await extensions.ParallelForEachAsync(async item =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractor = extractorFactory.ForExtractionStrategy(item.ExtractionStrategy);
            var containerFolderInBinary = GetContainerFolderInBinary(item.Source, extractor);
            var ignoreRootFolder = !string.IsNullOrEmpty(containerFolderInBinary);

            await extractor.ExtractAsync(
                item.Source,
                instanceDirectory,
                excludePredicate: item => !item.Key.StartsWith(containerFolderInBinary),
                ignoreRootFolder,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation($"Extracted {item.Source} into {instanceDirectory}");

        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="zipFile"></param>
    /// <param name="extractor"></param>
    /// <returns></returns>
    private string GetContainerFolderInBinary(string zipFile, IFileExtractor extractor)
    {
        //some of the extension binaries may have a root folder which need to be ignored while extracting content
        var containerFolder = "";

        var item = extractor
            .Enumerate(zipFile)
            .Where(entry => entry.Key.EndsWith("/bin/") || entry.Key.EndsWith("/lib/") || entry.Key.EndsWith("/share/"))
            .Select(entry => entry.Key)
            .FirstOrDefault();

        if (item == null)
            return containerFolder;

        var parts = item.Split('/');
        if (parts.Length > 1)
        {
            containerFolder = parts[0];
        }

        return containerFolder;
    }
}
