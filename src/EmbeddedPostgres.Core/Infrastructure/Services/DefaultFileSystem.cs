using EmbeddedPostgres.Core;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Services;

internal class DefaultFileSystem : IFileSystem
{
    private readonly object syncObj = new();

    /// <summary>
    /// Enumerates the file system entries in the specified path.
    /// </summary>
    /// <param name="path">The path to search for entries.</param>
    /// <param name="searchPattern">The search pattern to match file names; defaults to "*".</param>
    /// <param name="enumerationOptions">The options to use for enumeration; defaults to <c>default</c>.</param>
    /// <returns>An enumerable collection of file system entries.</returns>
    public IEnumerable<string> EnumerateFileSystemEntries(
      string path,
      string searchPattern,
      EnumerationOptions enumerationOptions = default)
    {
        return Directory.EnumerateFileSystemEntries(
          path,
          searchPattern,
          enumerationOptions ?? new EnumerationOptions()
          {
              MatchType = MatchType.Simple
          });
    }

    /// <summary>
    /// Copies a file from the source to the destination.
    /// </summary>
    /// <param name="sourceFileName">The path of the source file.</param>
    /// <param name="destFileName">The path of the destination file.</param>
    /// <param name="overwrite">Whether to overwrite the destination file if it exists; defaults to <c>false</c>.</param>
    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
    {
        File.Copy(sourceFileName, destFileName, overwrite);
    }


    /// <summary>
    /// Asynchronously copies the content of a stream to a file.
    /// </summary>
    /// <param name="stream">The stream to copy.</param>
    /// <param name="destFileName">The destination file path.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    public void CopyDirectory(string sourceDir, string destDir)
    {
        // Create the destination directory if it doesn't exist
        EnsureDirectory(destDir);

        // ExtractAsync all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            CopyFile(file, destFile, true); // Set true to overwrite if exists
        }

        // ExtractAsync all subdirectories
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(directory));
            CopyDirectory(directory, destSubDir); // Recursive call
        }
    }

    /// <summary>
    /// Asynchronously copies the content of a stream to a file.
    /// </summary>
    /// <param name="stream">The stream to copy.</param>
    /// <param name="destFileName">The destination file path.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    public async Task CopyStreamAsync(Stream stream, string destFileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var outputFileStream = File.Open(destFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
        await stream.CopyToAsync(outputFileStream, cancellationToken).ConfigureAwait(false);
        await outputFileStream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the file already exists. If it does, returns false.
    /// If it does not exist, creates the file and returns true.
    /// </summary>
    /// <param name="filePath">The path of the file to check and create.</param>
    /// <returns><c>true</c> if the file was created; otherwise, <c>false</c>.</returns>
    public bool Touch(string filePath)
    {
        lock (syncObj)
        {
            if (CheckPath(filePath) != PathType.DoesNotExist)
            {
                return false;
            }
            else
            {
                // Create the file to act as a marker/sentinel
                using (Open(filePath, FileMode.OpenOrCreate)) { }
                return true;
            }
        }
    }

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
    public Stream Open(
          string filePath,
          FileMode mode = FileMode.Open,
          FileAccess access = FileAccess.Read,
          FileShare share = FileShare.Read,
          int bufferSize = 64 * 1024,
          bool useAsync = false)
    {
        return new FileStream(filePath, mode, access, share, 64 * 1024, true);
    }

    /// <summary>
    /// Checks the specified path and determines if it does not exist, is a file, or is a directory.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>A <see cref="PathType"/> value indicating the type of the path.</returns>
    public PathType CheckPath(string path)
    {
        if (File.Exists(path))
        {
            return PathType.File;
        }
        else if (Directory.Exists(path))
        {
            return PathType.Directory;
        }
        else
        {
            return PathType.DoesNotExist;
        }
    }

    /// <summary>
    /// Deletes a file at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the file to delete.</param>
    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Deletes a directory at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the directory to delete.</param>
    public void DeleteDirectory(string filePath)
    {
        void DeleteDirectoryRecursively(string directoryPath)
        {
            // Delete all files in the directory
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }

            // Delete all subdirectories
            foreach (var subdirectory in Directory.EnumerateDirectories(directoryPath))
            {
                DeleteDirectoryRecursively(subdirectory);
            }

            // Delete the directory itself
            Directory.Delete(directoryPath, recursive: false);
        }

        if (Directory.Exists(filePath))
        {
            // Path is a directory
            DeleteDirectoryRecursively(filePath);
        }
    }

    /// <summary>
    /// Ensures that the specified directory exists; creates it if it does not.
    /// </summary>
    /// <param name="folderPath">The path of the folder to ensure exists.</param>
    public void EnsureDirectory(string folderPath)
    {
        var pathType = CheckPath(folderPath);

        if (pathType == PathType.File) 
        {
            throw new PgCoreException($"{folderPath} is a file!");
        }

        if (pathType == PathType.DoesNotExist)
            Directory.CreateDirectory(folderPath);
    }

    public FileEntryInfo GetFileEntryInfo(string path)
    {
        return new FileEntryInfo()
        {
            Attributes = File.GetAttributes(path),
            CreationTime = File.GetCreationTime(path),
            LastWriteTime = File.GetLastWriteTime(path),
        };
    }
}
