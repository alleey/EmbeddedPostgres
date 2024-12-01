using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres;

public class PgServerVersions
{
    public const string Latest = "17.0.0";
}

/// <summary>
/// Options for building a PostgreSQL server instance.
/// </summary>
public class PgServerBuilderOptions
{
    internal PgArtifact mainArtifact = new();
    internal List<PgArtifact> extensions = new();
    internal PgInstanceBuilderOptions instanceOptions = PgInstanceBuilderOptions.NamedInstance(Guid.NewGuid().ToString());
    internal List<PgDataClusterBuilderOptions> dataClusters = new();

    /// <summary>
    /// Gets the default options for building a PostgreSQL server instance.
    /// </summary>
    public static PgServerBuilderOptions Default { get; } = new();

    /// <summary>
    /// The version of PostgreSQL to be used, in the format x.y.z (e.g., 17.0.0).
    /// </summary>
    public string Version { get; set; } = PgServerVersions.Latest;

    /// <summary>
    /// The download URL for the main PostgreSQL binaries. This can be a minimal or standard binaries link,
    /// or a path to an already downloaded zip file. Note: Zonkyio minimal binaries are supported.
    /// </summary>
    /// <remarks>
    /// See <see cref="PgIoZonkyTestBinaries"/> and <see cref="PgStandardBinaries"/>.
    /// </remarks>
    public string ServerBinaries
    {
        get => mainArtifact.Source;
        set => mainArtifact = mainArtifact with { Source = value, IsLocal = PathChecker.IsLocalPath(value) };
    }

    /// <summary>
    /// Gets or sets the main PostgreSQL artifact.
    /// </summary>
    /// <remarks>
    /// See <see cref="PgIoZonkyTestBinaries"/> and <see cref="PgStandardBinaries"/>.
    /// </remarks>
    public PgArtifact ServerArtifact
    {
        get => mainArtifact;
        set => mainArtifact = value;
    }

    /// <summary>
    /// The directory where downloaded artifacts will be stored.
    /// </summary>
    public string CacheDirectory { get; set; } = ".embeddedpostgres";

    public PgInstanceBuilderOptions InstanceOptions
    {
        get => instanceOptions;
        set => instanceOptions = value;
    }

    /// <summary>
    /// The directory for the PostgreSQL instance.
    /// </summary>
    public string InstanceDirectory
    {
        get => instanceOptions.InstanceDirectory;
        set => instanceOptions = instanceOptions with { InstanceDirectory = value };
    }

    /// <summary>
    /// Indicates whether to perform a clean installation of PostgreSQL.
    /// </summary>
    public bool CleanInstall
    {
        get => instanceOptions.CleanInstall;
        set => instanceOptions = instanceOptions with { CleanInstall = value };
    }

    /// <summary>
    /// Indicates whether to exclude installation of pgAdmin (applies to standard binaries). This can save
    /// ~650 MB of disk space.
    /// </summary>
    public bool ExcludePgAdminInstallation { get; set; } = true;

    /// <summary>
    /// The parameter name for normalizing file attributes.
    /// </summary>
    public bool NormallizeAttributes { get; set; } = false;

    /// <summary>
    /// The parameter name for adding local user access permissions.
    /// </summary>
    public bool AddLocalUserAccessPermission { get; set; } = false;

    /// <summary>
    /// The parameter name for setting executable attributes on files.
    /// </summary>
    public bool SetExecutableAttributes { get; set; } = true;

    /// <summary>
    /// Adds an instance parameter to the PostgreSQL instance configuration.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    public void AddInstanceParameter(string name, object value)
    {
        instanceOptions.PlatformParameters.Add(name, value);
    }

    /// <summary>
    /// Adds a PostgreSQL extension by specifying its download URL or local file path.
    /// </summary>    
    /// <param name="source">The download URL or local path for the extension.</param>
    public void AddPostgresExtension(string source)
    {
        extensions.Add(new PgArtifact { Source = source, IsLocal = PathChecker.IsLocalPath(source) });
    }

    /// <summary>
    /// Gets the list of currently configured PostgreSQL data clusters.
    /// </summary>
    public IList<PgDataClusterBuilderOptions> DataClusters
    {
        get => dataClusters;
    }

    /// <summary>
    /// Adds a default PostgreSQL data cluster with default options.
    /// </summary>
    /// <remarks>
    /// This method uses a default configuration by invoking <see cref="AddDataCluster(Action{PgDataClusterBuilderOptions})"/>
    /// with no specific options.
    /// </remarks>
    public void AddDefaultDataCluster() => AddDataCluster(builder => { builder.UniqueId = "primary"; });

    /// <summary>
    /// Adds a PostgreSQL data cluster using the specified configuration options.
    /// </summary>
    /// <param name="builder">An action to configure the <see cref="PgDataClusterBuilderOptions"/> for the data cluster.</param>
    /// <exception cref="PgValidationException">
    /// Thrown if the specified options do not have a valid port, or if a cluster with the same host, port, and data directory already exists.
    /// </exception>
    public void AddDataCluster(Action<PgDataClusterBuilderOptions> builder)
    {
        var options = new PgDataClusterBuilderOptions();
        builder(options);
        dataClusters.Add(options);
    }
}
