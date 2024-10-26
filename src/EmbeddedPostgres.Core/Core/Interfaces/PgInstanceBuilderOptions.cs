namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the options for building a PostgreSQL instance, including configuration settings
/// inherited from <see cref="PgInstanceConfiguration"/>.
/// </summary>
public record PgInstanceBuilderOptions : PgInstanceConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to perform a clean installation of the PostgreSQL instance.
    /// Defaults to <c>false</c>. If set to <c>true</c>, existing data may be removed before installation.
    /// </summary>
    public bool CleanInstall { get; init; } = false;

    /// <summary>
    /// Indicates whether to exclude installation of pgAdmin (applies to standard binaries). This can save
    /// ~650 MB of disk space.
    /// </summary>
    public bool ExcludePgAdminInstallation { get; set; } = true;

    /// <summary>
    /// Creates a new instance of <see cref="PgInstanceBuilderOptions"/> with the specified instance name.
    /// </summary>
    /// <param name="instanceName">The name of the PostgreSQL instance.</param>
    /// <returns>A new <see cref="PgInstanceBuilderOptions"/> with the specified instance name.</returns>
    public static new PgInstanceBuilderOptions NamedInstance(string instanceName)
    {
        return new() { InstanceDirectory = instanceName };
    }
}
