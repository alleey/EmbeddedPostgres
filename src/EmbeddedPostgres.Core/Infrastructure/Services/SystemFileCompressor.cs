using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

/// <summary>
/// A file compressor implementation using System.IO.Compression.
/// Compresses files and directories into a compressed archive (ZIP format).
/// </summary>
internal class SystemFileCompressor : IFileCompressor
{
    private readonly IFileSystem fileSystem;

    public SystemFileCompressor(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Asynchronously compresses the specified source (file or directory) to a destination file.
    /// </summary>
    /// <param name="source">The path to the source file or directory to compress.</param>
    /// <param name="destFile">The destination file path for the compressed archive.</param>
    /// <param name="excludePredicate">An optional predicate to filter out files during compression. Takes the file path and file entry info as input.</param>
    /// <param name="includeRootDir">Determines whether the root directory of the source should be included in the compressed archive. Default is true.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete. Default is none.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    public Task CompressAsync(
        string source,
        string destFile,
        Func<string, FileEntryInfo, bool> excludePredicate = null,
        bool includeRootDir = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (fileSystem.CheckPath(source) == PathType.DoesNotExist)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            using var archive = ZipFile.Open(destFile, ZipArchiveMode.Create);
            var rootDir = includeRootDir ? Path.GetDirectoryName(source) : source;

            if (fileSystem.CheckPath(source) == PathType.File)
            {
                var info = fileSystem.GetFileEntryInfo(source);
                var relativeName = Path.GetFileName(source);

                archive.CreateEntryFromFile(
                    source,
                    relativeName,
                    CompressionLevel.Optimal);
            }
            else
            {
                EnumerationOptions enumerationOptions = new()
                {
                    RecurseSubdirectories = true,
                };

                foreach (var file in fileSystem.EnumerateFileSystemEntries(source, enumerationOptions: enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var info = fileSystem.GetFileEntryInfo(file);
                    var relativeName = Path.GetRelativePath(rootDir, file);

                    // Check if the entry is a directory and add an empty directory entry to the archive if so
                    if (fileSystem.CheckPath(file) == PathType.Directory)
                    {
                        if (!relativeName.EndsWith("/"))
                        {
                            relativeName += "/";
                        }
                        archive.CreateEntry(relativeName);
                        continue;
                    }

                    // Apply the filter if any
                    if (excludePredicate?.Invoke(file, info) == true)
                    {
                        continue;
                    }

                    // CreateTargetDatabase a compressed entry from file
                    archive.CreateEntryFromFile(
                        file,
                        relativeName,
                        CompressionLevel.Optimal);
                }
            }

        }, cancellationToken);
    }
}