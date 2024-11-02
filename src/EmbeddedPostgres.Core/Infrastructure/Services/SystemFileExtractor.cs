using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

internal class SystemFileExtractor : IFileExtractor
{
    private readonly IFileSystem fileSystem;

    public SystemFileExtractor(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Enumerates the entries in the specified archive.
    /// </summary>
    /// <param name="source">The path to the archive source.</param>
    /// <returns>A collection of <see cref="ArchiveEntry"/> representing the entries in the archive.</returns>
    public IEnumerable<ArchiveEntry> Enumerate(string source)
    {
        using var stream = fileSystem.Open(source, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            yield return new ArchiveEntry
            {
                Key = entry.FullName,
                IsDirectory = string.IsNullOrEmpty(entry.Name),
                Size = entry.Length
            };
        }
    }

    /// <summary>
    /// Extracts the contents of the specified archive to the designated directory.
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

        static ArchiveEntry ToArchiveEntry(ZipArchiveEntry entry)
        {
            return new ArchiveEntry { Key = entry.FullName, IsDirectory = string.IsNullOrEmpty(entry.Name), Size = entry.Length };
        }

        using var stream = fileSystem.Open(source, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip directories
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            // Apply the filter if any
            if ((excludePredicate?.Invoke(ToArchiveEntry(entry)) ?? false))
            {
                continue;
            }

            var key = entry.FullName;
            if (ignoreRootDir)
            {
                // Strip the root dir prefix from paths
                key = string.Join('/', key.Split('/').Skip(1));
            }

            // Specify the extraction path for the entry
            var extractionPath = Path.Combine(destDir, key);
            var targetDirectory = Path.GetDirectoryName(extractionPath);

            fileSystem.EnsureDirectory(targetDirectory);

            using (var entryStream = entry.Open())
            {
                await fileSystem.CopyStreamAsync(entryStream, extractionPath, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
