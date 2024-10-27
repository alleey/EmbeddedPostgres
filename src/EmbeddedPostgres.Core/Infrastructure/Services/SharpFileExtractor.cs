using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

internal class SharpFileExtractor : IFileExtractor
{
    private readonly IFileSystem fileSystem;

    public SharpFileExtractor(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <summary>
    /// Enumerates the entries in the specified archive.
    /// </summary>
    /// <param name="source">The path to the archive source.</param>
    /// <returns>A collection of <see cref="ArchiveEntry"/> representing the entries in the archive.</returns>
    public IEnumerable<ArchiveEntry> Enumerate(string source)
    {
        using var stream = fileSystem.Open(source, FileMode.Open, FileAccess.Read);
        using var reader = ReaderFactory.Open(stream);

        while (reader.MoveToNextEntry())
        {
            yield return new ArchiveEntry 
            {
                Key = reader.Entry.Key,
                IsDirectory = reader.Entry.IsDirectory,
                Size = reader.Entry.Size
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

        static ArchiveEntry ToArchiveEntry(SharpCompress.Common.IEntry entry)
        {
            return new ArchiveEntry { Key = entry.Key, IsDirectory = entry.IsDirectory, Size = entry.Size };
        }

        var symbolicLinks = new Dictionary<string, string>();

        using var stream = fileSystem.Open(source, FileMode.Open, FileAccess.Read);
        using var reader = ReaderFactory.Open(stream);

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = reader.Entry;
            var key = entry.Key;

            if (ignoreRootDir)
            {
                // Strip the root dir prefix from paths
                key = string.Join('/', key.Split('/').Skip(1));
            }

            if (entry.IsDirectory)
            {
                fileSystem.EnsureDirectory(Path.Combine(destDir, key));
                continue;
            }

            // Apply the filter if any
            if ((excludePredicate?.Invoke(ToArchiveEntry(entry)) ?? false))
            {
                continue;
            }

            if (entry.LinkTarget != null)
            {
                symbolicLinks[key] = entry.LinkTarget;
                continue;
            }

            // Specify the extraction path for the entry
            var extractionPath = Path.Combine(destDir, key);
            var targetDirectory = Path.GetDirectoryName(extractionPath);

            fileSystem.EnsureDirectory(targetDirectory);

            // For some reason CopyStream hangs for zero length files
            if (entry.Size == 0)
            {
                fileSystem.Touch(extractionPath);
            }
            else
            {
                using var entryStream = reader.OpenEntryStream();
                await fileSystem.CopyStreamAsync(entryStream, extractionPath, cancellationToken).ConfigureAwait(false);
            }
        }

        // Right now handle symlinks the poor way i.e. copy the source. Need to handle this better
        // use reparse points on Windows, soft links on linux
        //
        foreach (var item in symbolicLinks)
        {
            var extractionPath = Path.Combine(destDir, item.Key);
            var sourcePath = Path.Combine(Path.GetDirectoryName(extractionPath), item.Value);

            var isDirectory = fileSystem.DirectoryExists(sourcePath);

            if (!isDirectory)
            {
                var targetDirectory = Path.GetDirectoryName(extractionPath);
                fileSystem.EnsureDirectory(targetDirectory);

                fileSystem.CopyFile(sourcePath, extractionPath, true);
            }
            else
            {
                fileSystem.CopyDirectory(sourcePath, extractionPath);
            }
        }
    }
}
