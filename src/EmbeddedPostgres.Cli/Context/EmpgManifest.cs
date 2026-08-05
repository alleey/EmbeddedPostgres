using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// Describes a single data cluster belonging to an instance.
/// </summary>
public class EmpgClusterEntry
{
    /// <summary>
    /// The name used to address this cluster on the command line.
    /// </summary>
    public string Id { get; set; } = "primary";

    /// <summary>
    /// Data directory, relative to the instance directory.
    /// </summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>
    /// The address clients are told to connect to. This is not what the server binds to — see
    /// <see cref="ListenAddresses"/>.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Value for PostgreSQL's <c>listen_addresses</c>: the interfaces the server binds to.
    /// <c>null</c> leaves PostgreSQL's own default, which is loopback only.
    /// </summary>
    public string? ListenAddresses { get; set; }

    /// <summary>
    /// When true, the cluster runs with fsync and friends enabled.
    /// </summary>
    /// <remarks>
    /// The library starts every cluster with <c>-F</c>, which disables fsync — appropriate for
    /// throwaway test clusters and not for anything holding data you care about. Durable clusters
    /// override that back on, so the unsafe setting has to be chosen deliberately.
    /// </remarks>
    public bool Durable { get; set; } = true;

    public int Port { get; set; }

    public string Superuser { get; set; } = "postgres";

    public string Encoding { get; set; } = "UTF-8";

    public string? Locale { get; set; }

    /// <summary>
    /// postgresql.conf parameters applied when the cluster starts.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// The on-disk description of an empg instance, stored at <c>.empg/manifest.json</c>.
/// </summary>
/// <remarks>
/// The library reconstructs an environment from options supplied at call time and does not
/// persist cluster metadata such as port, superuser or id. The manifest is what lets commands
/// operate on an existing instance without repeating those values on every invocation.
/// </remarks>
public class EmpgManifest
{
    /// <summary>
    /// Name of the directory holding CLI-owned state inside an instance.
    /// </summary>
    public const string DirectoryName = ".empg";

    public const string FileName = "manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Read manifests written before the camelCase policy was adopted.
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Manifest schema version, so future CLI releases can migrate older instances.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// True when the binaries were already on disk and empg only adopted them.
    /// </summary>
    /// <remarks>
    /// Destructive commands consult this so that empg only ever deletes what it installed.
    /// </remarks>
    public bool Adopted { get; set; }

    /// <summary>
    /// How the instance came to exist, as shown to the user: <c>adopted</c> when the binaries were
    /// already on disk, <c>managed</c> when empg installed them.
    /// </summary>
    [JsonIgnore]
    public string Kind => Adopted ? "adopted" : "managed";

    /// <summary>
    /// PostgreSQL version this instance was provisioned with.
    /// </summary>
    public string? PostgresVersion { get; set; }

    /// <summary>
    /// The URL or local path the server binaries were installed from.
    /// </summary>
    public string? ServerArtifact { get; set; }

    /// <summary>
    /// Directory holding downloaded artifacts, relative to the instance directory.
    /// </summary>
    public string CacheDirectory { get; set; } = Path.Combine(DirectoryName, "cache");

    /// <summary>
    /// URLs or paths of extensions installed into this instance.
    /// </summary>
    public List<string> Extensions { get; set; } = new();

    public List<EmpgClusterEntry> Clusters { get; set; } = new();

    /// <summary>
    /// Returns the cluster with the given id, or <c>null</c> when no such cluster exists.
    /// </summary>
    public EmpgClusterEntry? FindCluster(string id)
        => Clusters.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string GetManifestPath(string instanceDirectory)
        => Path.Combine(instanceDirectory, DirectoryName, FileName);

    public static EmpgManifest Load(string instanceDirectory)
    {
        var path = GetManifestPath(instanceDirectory);
        return JsonSerializer.Deserialize<EmpgManifest>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException($"Manifest at {path} is empty or malformed.");
    }

    /// <summary>
    /// Writes the manifest, replacing any existing one atomically.
    /// </summary>
    /// <remarks>
    /// The manifest is the only record of an instance's clusters, so a half-written file would
    /// strand them. Serializing to a temporary file first means an interrupted write leaves the
    /// previous manifest intact rather than a truncated one.
    /// </remarks>
    public void Save(string instanceDirectory)
    {
        var path = GetManifestPath(instanceDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, SerializerOptions));

        // File.Move replaces the destination in one step; Replace would fail when none exists yet.
        File.Move(temporaryPath, path, overwrite: true);
    }
}
