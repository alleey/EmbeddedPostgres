using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres;

public record PgDataClusterBuilderOptions
{
    internal PgDataClusterConfiguration configuration = PgDataClusterConfiguration.Default;

    /// <summary>
    /// Gets the default configuration for a PostgreSQL server instance.
    /// </summary>
    public static PgDataClusterBuilderOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the PostgreSQL server configuration.
    /// </summary>
    public PgDataClusterConfiguration Configuration
    {
        get => configuration;
        set => configuration = value;
    }

    /// <summary>
    /// The data directory for the PostgreSQL instance.
    /// </summary>
    public string UniqueId
    {
        get => configuration.UniqueId;
        set => configuration = configuration with { UniqueId = value };
    }

    /// <summary>
    /// The data directory for the PostgreSQL instance.
    /// </summary>
    public string DataDirectory
    {
        get => configuration.DataDirectory;
        set => configuration = configuration with { DataDirectory = value };
    }

    /// <summary>
    /// Gets or sets the superuser name for PostgreSQL.
    /// </summary>
    public string Superuser
    {
        get => configuration.Superuser;
        set => configuration = configuration with { Superuser = value };
    }

    /// <summary>
    /// Gets or sets the locale for the PostgreSQL server.
    /// </summary>
    public string Locale
    {
        get => configuration.Locale;
        set => configuration = configuration with { Locale = value };
    }

    /// <summary>
    /// Gets or sets the encoding for the PostgreSQL server.
    /// </summary>
    public string Encoding
    {
        get => configuration.Encoding;
        set => configuration = configuration with { Encoding = value };
    }

    /// <summary>
    /// Gets or sets a value indicating whether to allow group access for the PostgreSQL server.
    /// </summary>
    public bool? AllowGroupAccess
    {
        get => configuration.AllowGroupAccess;
        set => configuration = configuration with { AllowGroupAccess = value };
    }

    /// <summary>
    /// Gets or sets the host for the PostgreSQL server.
    /// </summary>
    public string Host
    {
        get => configuration.Host;
        set => configuration = configuration with { Host = value };
    }

    /// <summary>
    /// Gets or sets the port for the PostgreSQL server.
    /// </summary>
    public int Port
    {
        get => configuration.Port;
        set => configuration = configuration with { Port = value };
    }

    /// <summary>
    /// Adds a server parameter to the PostgreSQL configuration.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    public void AddServerParameter(string name, string value)
    {
        configuration.Parameters.Add(name, value);
    }

    /// <summary>
    /// Adds multiple server parameters to the PostgreSQL configuration.
    /// </summary>
    /// <param name="parameters">A collection of key-value pairs representing server parameters.</param>
    public void AddClusterParameters(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        foreach (var parameter in parameters)
            configuration.Parameters.Add(parameter.Key, parameter.Value);
    }

}
