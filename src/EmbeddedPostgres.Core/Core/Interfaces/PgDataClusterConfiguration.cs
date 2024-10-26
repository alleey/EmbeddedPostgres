using System;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the configuration settings for a PostgreSQL server instance.
/// </summary>
public record PgDataClusterConfiguration
{
    /// <summary>
    /// Gets the default configuration for a PostgreSQL server instance.
    /// </summary>
    public static PgDataClusterConfiguration Default { get; } = new();

    /// <summary>
    /// Allows you to attach a unique id with the cluster that can be used for identification. 
    /// Note: this is an application level Id only, the underlying Postgres system doesnt
    /// have anything to do with it.
    /// </summary>
    public string UniqueId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the directory where the PostgreSQL data will be stored.
    /// Defaults to "data".
    /// </summary>
    public string DataDirectory { get; init; } = "data";

    /// <summary>
    /// Gets or sets the name of the superuser for the PostgreSQL server. 
    /// Defaults to "postgres".
    /// </summary>
    public string Superuser { get; init; } = "postgres";

    // InitDB configuration
    /// <summary>
    /// Gets or sets the locale setting for the database. 
    /// This can influence language and formatting settings. Defaults to <c>null</c>.
    /// </summary>
    public string Locale { get; init; } = null;

    /// <summary>
    /// Gets or sets the character encoding for the database. 
    /// Defaults to "UTF-8".
    /// </summary>
    public string Encoding { get; init; } = "UTF-8";

    /// <summary>
    /// Gets or sets a value indicating whether group access is allowed for the database.
    /// This setting can be <c>null</c>, true, or false.
    /// </summary>
    public bool? AllowGroupAccess { get; init; } = null;

    // PostgreSQL server configuration
    /// <summary>
    /// Gets or sets the hostname or IP address of the PostgreSQL server. 
    /// Defaults to "localhost".
    /// </summary>
    public string Host { get; init; } = "localhost";

    /// <summary>
    /// Gets or sets the port number on which the PostgreSQL server is listening. 
    /// Defaults to 0, indicating that the server will choose an ephemeral port.
    /// </summary>
    public int Port { get; init; } = 0;

    /// <summary>
    /// Gets or sets additional parameters for the PostgreSQL server configuration.
    /// This can be used to pass extra configuration options as key-value pairs.
    /// </summary>
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}
