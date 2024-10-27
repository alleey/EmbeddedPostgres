using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

/// <summary>
/// Represents an entry in an archive, which can be a file or a directory.
/// </summary>
public record ArchiveEntry
{
    /// <summary>
    /// Gets the key or path of the archive entry.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Indicates whether the entry is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Gets the size of the entry in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Indicates whether the entry has a valid key.
    /// </summary>
    public bool HasKey => !string.IsNullOrEmpty(Key);

    /// <summary>
    /// Gets the file extension of the entry if it is a file; otherwise, returns <c>null</c>.
    /// </summary>
    public string Extension => (HasKey && !IsDirectory) ? Path.GetExtension(Key) : default;
}

/// <summary>
/// Defines a contract for extracting files from an archive.
/// </summary>
public interface IFileExtractor
{
    /// <summary>
    /// Enumerates the entries in the specified archive.
    /// </summary>
    /// <param name="source">The path to the archive source.</param>
    /// <returns>A collection of <see cref="ArchiveEntry"/> representing the entries in the archive.</returns>
    IEnumerable<ArchiveEntry> Enumerate(string source);

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
    Task ExtractAsync(
        string source,
        string destDir,
        Func<ArchiveEntry, bool> excludePredicate = null,
        bool ignoreRootDir = false,
        CancellationToken cancellationToken = default);
}
