using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

internal class ZonkyFileExtractor : IFileExtractor
{
    private readonly Func<IFileExtractorFactory> extractorFactoryFactory;
    private readonly IFileSystem fileSystem;

    public ZonkyFileExtractor(Func<IFileExtractorFactory> extractorFactoryFactory,  IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(extractorFactoryFactory, nameof(extractorFactoryFactory));

        this.extractorFactoryFactory = extractorFactoryFactory ?? throw new ArgumentNullException(nameof(extractorFactoryFactory));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Enumerates the entries in the specified archive.
    /// </summary>
    /// <param name="source">The path to the archive source.</param>
    /// <returns>A collection of <see cref="ArchiveEntry"/> representing the entries in the archive.</returns>
    public IEnumerable<ArchiveEntry> Enumerate(string source)
    {
        var extractor = extractorFactoryFactory().ForFileType(source);
        return extractor.Enumerate(source);
    }

    /// <summary>
    /// The Zonky Test minimal binaries are jar files with a nested txf zip
    /// </summary>
    /// <param name="source">The path to the archive source.</param>
    /// <param name="destDir">The destination directory where the files will be extracted.</param>
    /// <param name="excludePredicate">
    /// An optional predicate function to determine if an entry should be excluded from extraction.
    /// </param>
    /// <param name="ignoreRootDir">
    /// Indicates whether to ignore the root directory of the archive during extraction.
    /// </param>
    /// <param name="cancellationToken">A token to signal cancellation of the extraction operation.</param>
    /// <returns>A task representing the asynchronous extraction operation.</returns>
    public async Task ExtractAsync(
        string source,
        string destDir,
        Func<ArchiveEntry, bool> excludePredicate = null,
        bool ignoreRootDir = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var factory = extractorFactoryFactory();
        var extractor = factory.ForFileType(source);
        var txzFile = extractor
            .Enumerate(source)
            .Where(item => (item.Extension?.ToLower() ?? string.Empty) == ".txz")
            .Select(item => item.Key)
            .FirstOrDefault();

        // If there is a txf file
        if (txzFile != null)
        {
            var zipFileDir = Path.GetDirectoryName(source);
            if (!fileSystem.FileExists(zipFileDir))
            {
                await extractor.ExtractAsync(source, zipFileDir, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            source = Path.Combine(zipFileDir, txzFile);
        }

        extractor = factory.ForFileType(source);
        await extractor.ExtractAsync(
            source,
            destDir,
            ignoreRootDir: false,
            excludePredicate: excludePredicate,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
