using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

public interface IFileCompressor
{
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
    Task CompressAsync(
        string source,
        string destFile,
        Func<string, FileEntryInfo, bool> excludePredicate = null,
        bool includeRootDir = true,
        CancellationToken cancellationToken = default);
}