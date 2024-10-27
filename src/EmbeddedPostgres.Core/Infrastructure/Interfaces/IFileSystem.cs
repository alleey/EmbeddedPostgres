using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

/// <summary>
/// Represents the type of a path in the file system.
/// </summary>
[Flags]
public enum PathType
{
    /// <summary>
    /// Indicates that the path does not exist.
    /// </summary>
    DoesNotExist = 0,

    /// <summary>
    /// Indicates that the path is a file.
    /// </summary>
    File = 1,

    /// <summary>
    /// Indicates that the path is a directory.
    /// </summary>
    Directory = 2,

    /// <summary>
    /// Indicates that the path can be either a file or a directory.
    /// </summary>
    FileOrDirectory = 3
}

public record FileEntryInfo
{
    public FileAttributes Attributes { get; init; }
    public DateTime CreationTime { get; init; }
    public DateTime LastWriteTime { get; init; }
}

/// <summary>
/// Represents a file system abstraction that provides methods for file and directory operations.
/// </summary>
public interface IFileSystem
{
    public FileEntryInfo GetFileEntryInfo(string path);

    /// <summary>
    /// Ensures that the specified directory exists; creates it if it does not.
    /// </summary>
    /// <param name="folderPath">The path of the folder to ensure exists.</param>
    void EnsureDirectory(string folderPath);

    /// <summary>
    /// Checks if the file already exists. If it does, returns false.
    /// If it does not exist, creates the file and returns true.
    /// </summary>
    /// <param name="filePath">The path of the file to check and create.</param>
    /// <returns><c>true</c> if the file was created; otherwise, <c>false</c>.</returns>
    bool Touch(string filePath);

    /// <summary>
    /// Enumerates the file system entries in the specified path.
    /// </summary>
    /// <param name="path">The path to search for entries.</param>
    /// <param name="searchPattern">The search pattern to match file names; defaults to "*".</param>
    /// <param name="enumerationOptions">The options to use for enumeration; defaults to <c>default</c>.</param>
    /// <returns>An enumerable collection of file system entries.</returns>
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", EnumerationOptions enumerationOptions = default);

    /// <summary>
    /// Checks the specified path and determines if it does not exist, is a file, or is a directory.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>A <see cref="PathType"/> value indicating the type of the path.</returns>
    PathType CheckPath(string path);

    /// <summary>
    /// Asynchronously copies the content of a stream to a file.
    /// </summary>
    /// <param name="stream">The stream to copy.</param>
    /// <param name="destFileName">The destination file path.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    Task CopyStreamAsync(Stream stream, string destFileName, CancellationToken cancellationToken);

    /// <summary>
    /// Copies a file from the source to the destination.
    /// </summary>
    /// <param name="sourceFileName">The path of the source file.</param>
    /// <param name="destFileName">The path of the destination file.</param>
    /// <param name="overwrite">Whether to overwrite the destination file if it exists; defaults to <c>false</c>.</param>
    void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);

    /// <summary>
    /// Copies all contents of a directory to a new destination directory.
    /// </summary>
    /// <param name="sourceDir">The source directory path.</param>
    /// <param name="destDir">The destination directory path.</param>
    void CopyDirectory(string sourceDir, string destDir);

    /// <summary>
    /// Opens a file with the specified parameters.
    /// </summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="mode">The mode in which to open the file; defaults to <see cref="FileMode.Open"/>.</param>
    /// <param name="access">The access level for the file; defaults to <see cref="FileAccess.Read"/>.</param>
    /// <param name="share">The sharing mode for the file; defaults to <see cref="FileShare.Read"/>.</param>
    /// <param name="bufferSize">The size of the buffer to use; defaults to 64 KB.</param>
    /// <param name="useAsync">Whether to use asynchronous file operations; defaults to <c>false</c>.</param>
    /// <returns>A stream representing the opened file.</returns>
    Stream Open(string filePath,
        FileMode mode = FileMode.Open,
        FileAccess access = FileAccess.Read,
        FileShare share = FileShare.Read,
        int bufferSize = 64 * 1024,
        bool useAsync = false);

    /// <summary>
    /// Deletes a file at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the file to delete.</param>
    void DeleteFile(string filePath);

    /// <summary>
    /// Deletes a directory at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the directory to delete.</param>
    void DeleteDirectory(string filePath);
}
