using System;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the configuration settings for a PostgreSQL instance.
/// </summary>
public record PgInstanceConfiguration
{
    /// <summary>
    /// Gets or sets the directory where the PostgreSQL instance is located.
    /// This is a required property and must be specified during configuration.
    /// </summary>
    public string InstanceDirectory { get; init; }

    /// <summary>
    /// Gets or sets additional parameters specific to the platform.
    /// This can be used to pass extra configuration options as key-value pairs.
    /// </summary>
    public IDictionary<string, object> PlatformParameters { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the default configuration for a PostgreSQL instance.
    /// A new instance name is generated using a GUID.
    /// </summary>
    public static PgInstanceConfiguration Default { get; } = NamedInstance(Guid.NewGuid().ToString());

    /// <summary>
    /// Creates a new instance of <see cref="PgInstanceConfiguration"/> with a specified instance name.
    /// </summary>
    /// <param name="instanceName">The name of the PostgreSQL instance.</param>
    /// <returns>A configured <see cref="PgInstanceConfiguration"/> instance.</returns>
    public static PgInstanceConfiguration NamedInstance(string instanceName)
    {
        return new() { InstanceDirectory = instanceName };
    }
}
