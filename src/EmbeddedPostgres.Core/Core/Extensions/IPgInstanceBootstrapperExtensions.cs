using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Extensions;

internal static class IPgInstanceBootstrapperExtensions
{
    /// <summary>
    /// Sets the file attributes of all entries in the specified directory to <see cref="FileAttributes.Normal"/>.
    /// This operation is performed asynchronously, allowing for parallel processing of entries.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance used to enumerate the file system entries.</param>
    /// <param name="directoryPath">The path of the directory whose file attributes are to be normalized.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="fileSystem"/> or <paramref name="directoryPath"/> is null.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the caller does not have the required permission to change the file attributes.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown if the specified directory does not exist.</exception>
    public static Task NormalizeAttributesAsync(this IFileSystem fileSystem, string directoryPath, CancellationToken cancellationToken = default)
    {
        return fileSystem.EnumerateFileSystemEntries(directoryPath).ParallelForEachAsync(
            item =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.SetAttributes(item, FileAttributes.Normal);
                return Task.CompletedTask;
            },
            maxDop: 32,
            cancellationToken: cancellationToken
        );
    }
}
