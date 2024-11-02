using EmbeddedPostgres.Infrastructure.Interfaces;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Services;

/// <summary>
/// A file compressor implementation using SharpCompress library.
/// Compresses files and directories into a compressed archive (ZIP format with BZip2 compression).
/// </summary>
internal class SharpFileCompressor : IFileCompressor
{
    private readonly IFileSystem fileSystem;

    public SharpFileCompressor(IFileSystem fileSystem)
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
            using var stream = fileSystem.Open(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.LZMA);
            var rootDir = includeRootDir ? Path.GetDirectoryName(source) : source;

            if (fileSystem.CheckPath(source) == PathType.File)
            {
                var info = fileSystem.GetFileEntryInfo(source);
                var relativeName = Path.GetFileName(source);

                writer.Write(
                    relativeName,
                    fileSystem.Open(source, FileMode.Open, FileAccess.Read),
                    info.LastWriteTime);
            }
            else
            {
                EnumerationOptions enumerationOptions = new ()
                {
                    RecurseSubdirectories = true,
                };

                foreach (var file in fileSystem.EnumerateFileSystemEntries(source, enumerationOptions: enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Console.WriteLine($"Compress {file}");

                    var info = fileSystem.GetFileEntryInfo(destFile);
                    var relativeName = Path.GetRelativePath(rootDir, file);

                    if (fileSystem.CheckPath(file) == PathType.Directory)
                    {
                        // SharpCompress doenst support empty directory entries

                        //writer.Write(relativeName + "/", Stream.Null, DateTime.Now);
                        continue;
                    }

                    // Apply the filter if any
                    if ((excludePredicate?.Invoke(file, info) ?? false))
                    {
                        continue;
                    }

                    writer.Write(
                        relativeName,
                        fileSystem.Open(file, FileMode.Open, FileAccess.Read),
                        info.LastWriteTime);
                }
            }

        }, cancellationToken);
    }
}