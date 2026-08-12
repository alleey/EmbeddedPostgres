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
            logger.LogInformation($"DropTargetDatabase install is set. Deleting {options.InstanceDirectory}");
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

        fileSystem.EnsureDirectory(instanceDirectory);

        // Installing extensions over an instance that already has its server binaries is a normal
        // operation, so a set of artifacts carrying no main archive is valid rather than an error.
        var pgServer = downloaded.FirstOrDefault(item => item.Kind == PgArtifactKind.Main);
        if (pgServer != null)
        {
            var extractor = extractorFactory.ForExtractionStrategy(pgServer.ExtractionStrategy);

            // Excluding pgAdmin extraction can save ~650Mb of disk space
            Func<ArchiveEntry, bool> excludePgAdminPredicate = item => item.Key.StartsWith("pgsql/pgAdmin");
            await extractor.ExtractAsync(
                pgServer.Source,
                instanceDirectory,
                excludePredicate: excludePgAdmin ? excludePgAdminPredicate : null,
                ignoreRootDir: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation($"Extracted {pgServer.Source} into {instanceDirectory}");
        }

        // Install all extensions, lets do that in parallel
        var extensions = downloaded.Where(item => item.Kind != PgArtifactKind.Main);
        await extensions.ParallelForEachAsync(async item =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractor = extractorFactory.ForExtractionStrategy(item.ExtractionStrategy);
            var layout = ResolveExtensionLayout(item.Source, extractor);

            await extractor.ExtractAsync(
                item.Source,
                instanceDirectory,
                excludePredicate: layout.BuildExcludePredicate(),
                layout.HasContainerFolder,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation($"Extracted {layout.FileCount} files from {item.Source} into {instanceDirectory}");

        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Where an extension archive keeps the files that belong in the instance, and how many there are.
    /// </summary>
    /// <param name="ContainerFolder">
    /// The single root folder the payload sits under, or an empty string when it sits at the archive root.
    /// </param>
    /// <param name="FileCount">Number of files that will be extracted.</param>
    private sealed record ExtensionLayout(string ContainerFolder, int FileCount)
    {
        public bool HasContainerFolder => !string.IsNullOrEmpty(ContainerFolder);

        /// <summary>
        /// Keeps only the payload. A null predicate takes the archive whole, which is what an archive
        /// with no wrapping folder needs.
        /// </summary>
        public Func<ArchiveEntry, bool> BuildExcludePredicate()
        {
            if (!HasContainerFolder)
            {
                return null;
            }

            var prefix = ContainerFolder + "/";
            return entry => !entry.Key.StartsWith(prefix);
        }
    }

    /// <summary>
    /// Works out where an extension archive keeps the directories PostgreSQL expects, so the payload
    /// lands merged into the instance rather than nested inside a stray folder.
    /// </summary>
    /// <remarks>
    /// The layout is settled from the paths of the entries themselves rather than from directory entries,
    /// which many archives omit entirely. An archive whose payload cannot be located is rejected instead
    /// of extracted: unpacking it somewhere PostgreSQL will not look would otherwise be reported as a
    /// successful install and only show up later as a missing extension.
    /// </remarks>
    /// <exception cref="PgCoreException">Thrown when the archive holds no recognisable payload.</exception>
    private ExtensionLayout ResolveExtensionLayout(string source, IFileExtractor extractor)
    {
        static bool IsPayloadDirectory(string segment)
            => segment is "bin" or "lib" or "share";

        var keys = extractor
            .Enumerate(source)
            .Where(entry => entry.HasKey)
            .Select(entry => new { entry.Key, Segments = entry.Key.Split('/', StringSplitOptions.RemoveEmptyEntries), entry.IsDirectory })
            .ToList();

        // Payload at the archive root, for example "lib/postgis-3.dll".
        if (keys.Any(entry => entry.Segments.Length > 1 && IsPayloadDirectory(entry.Segments[0])))
        {
            return new ExtensionLayout(string.Empty, keys.Count(entry => !entry.IsDirectory));
        }

        // Payload under a single wrapping folder, for example "postgis-bundle-pg17/lib/postgis-3.dll".
        var container = keys
            .Where(entry => entry.Segments.Length > 2 && IsPayloadDirectory(entry.Segments[1]))
            .Select(entry => entry.Segments[0])
            .FirstOrDefault();

        if (container != null)
        {
            var prefix = container + "/";
            return new ExtensionLayout(container, keys.Count(entry => !entry.IsDirectory && entry.Key.StartsWith(prefix)));
        }

        throw new PgCoreException(
            $"{source} does not look like a PostgreSQL extension archive: no bin, lib or share directory was found in it.");
    }
}
