using EmbeddedPostgres.Infrastructure.Interfaces;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the configuration and runtime environment for a PostgreSQL instance.
/// </summary>
public record PgEnvironment
{
    /// <summary>
    /// Gets the configuration settings for the PostgreSQL instance.
    /// </summary>
    public PgInstanceConfiguration Instance { get; init; }

    /// <summary>
    /// Get the list of Data clusters configured for the environment
    /// </summary>
    public IList<PgDataClusterConfiguration> DataClusters { get; init; } = new List<PgDataClusterConfiguration>();

    /// <summary>
    /// Gets the controller responsible for initializing the PostgreSQL database.
    /// </summary>
    public IPgInitDbController InitDbController { get; init; }

    /// <summary>
    /// Gets the controller responsible for managing the PostgreSQL server.
    /// </summary>
    public IPgDataClusterController DataClusterController { get; init; }

    /// <summary>
    /// Gets the SQL client controller used for executing SQL commands against the PostgreSQL database.
    /// </summary>
    public IPgSqlController SqlController { get; init; }

    /// <summary>
    /// Gets the controller responsible for restoring a PostgreSQL database from a dump.
    /// </summary>
    public IPgRestoreController RestoreController { get; init; }

    /// <summary>
    /// Gets the controller responsible for creating database dumps in PostgreSQL.
    /// </summary>
    public IPgDumpController DumpController { get; init; }

    /// <summary>
    /// Provides access to the file system for performing operations such as reading, writing, and managing files and directories.
    /// </summary>
    public IFileSystem FileSystem { get; init; }

    /// <summary>
    /// Handles file compression operations, enabling the creation of compressed file archives.
    /// </summary>
    public IFileCompressor FileCompressor { get; init; }

    /// <summary>
    /// Provides a factory for creating instances of file extractors used to decompress files or archives.
    /// </summary>
    public IFileExtractorFactory FileExtractorFactory { get; init; }

    /// <summary>
    /// Executes shell commands or external processes, providing an abstraction for system-level command execution.
    /// </summary>
    public ICommandExecutor CommandExecutor { get; init; }

    /// <summary>
    /// Helper service that can help download stuff
    /// </summary>
    public IHttpService HttpService { get; init; }

    /// <summary>
    /// Gets additional data or metadata associated with the PostgreSQL environment.
    /// This can be used to store custom properties that do not fit into the other configurations.
    /// </summary>
    public IDictionary<string, object> Extra { get; init; } = new Dictionary<string, object>();
}
