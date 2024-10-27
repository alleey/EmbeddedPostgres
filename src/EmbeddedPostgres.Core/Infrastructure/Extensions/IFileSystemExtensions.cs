using EmbeddedPostgres.Core;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EmbeddedPostgres.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IFileSystem"/> interface to simplify file and directory validations.
/// </summary>
public static class IFileSystemExtensions
{
    /// <summary>
    /// Requires that the specified path points to an existing file.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <exception cref="PgCoreException">Thrown when the path does not exist or is not a file.</exception>
    public static void RequireFile(this IFileSystem fileSystem, string path)
    {
        if (fileSystem.CheckPath(path) != PathType.File)
        {
            throw new PgCoreException($"File {path} does not exist or is not a file");
        }
    }

    /// <summary>
    /// Requires that the specified path does not point to an existing file.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <exception cref="PgCoreException">Thrown when the path points to an existing file.</exception>
    public static void RequireNotFile(this IFileSystem fileSystem, string path)
    {
        if (fileSystem.CheckPath(path) == PathType.File)
        {
            throw new PgCoreException($"{path} is supposed to be a file or non-existent. A directory with the same name exists however");
        }
    }

    /// <summary>
    /// Checks if the specified path points to an existing file.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
    public static bool FileExists(this IFileSystem fileSystem, string path)
    {
        return fileSystem.CheckPath(path) == PathType.File;
    }

    /// <summary>
    /// Requires that the specified path points to an existing directory.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <exception cref="PgCoreException">Thrown when the path does not exist or is not a directory.</exception>
    public static void RequireDirectory(this IFileSystem fileSystem, string path)
    {
        if (fileSystem.CheckPath(path) != PathType.Directory)
        {
            throw new PgCoreException($"Directory {path} does not exist or is not a directory");
        }
    }

    /// <summary>
    /// Requires that the specified path does not point to an existing directory.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <exception cref="PgCoreException">Thrown when the path points to an existing directory.</exception>
    public static void RequireNotDirectory(this IFileSystem fileSystem, string path)
    {
        if (fileSystem.CheckPath(path) == PathType.Directory)
        {
            throw new PgCoreException($"{path} is supposed to be a directory or non-existent. A file with the same name exists however");
        }
    }

    /// <summary>
    /// Checks if the specified path points to an existing directory.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
    public static bool DirectoryExists(this IFileSystem fileSystem, string path)
    {
        return fileSystem.CheckPath(path) == PathType.Directory;
    }

    /// <summary>
    /// Checks if the specified path points to an existing file or directory.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the file or directory exists; otherwise, <c>false</c>.</returns>
    public static bool FileOrDirectoryExists(this IFileSystem fileSystem, string path)
    {
        return fileSystem.CheckPath(path) != PathType.DoesNotExist;
    }

    /// <summary>
    /// Converts a filename to a valid format by removing invalid characters and appending a hash if necessary.
    /// </summary>
    /// <param name="fileSystem">The <see cref="IFileSystem"/> instance.</param>
    /// <param name="filename">The original filename to convert.</param>
    /// <returns>A valid filename that does not contain invalid characters.</returns>
    public static string ConvertToValidFilename(this IFileSystem fileSystem, string filename)
    {
        static string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        string validFileName = string.Concat(
            filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
        );

        if (filename == validFileName)
        {
            return filename;
        }

        var hash = ComputeHash(filename);
        return $"{validFileName}_{hash}";
    }
}
