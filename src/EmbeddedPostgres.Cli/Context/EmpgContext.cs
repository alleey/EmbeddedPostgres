using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// A resolved empg instance: the directory holding the PostgreSQL binaries together with
/// the manifest describing its clusters.
/// </summary>
public class EmpgContext
{
    public EmpgContext(string instanceDirectory, EmpgManifest manifest)
    {
        InstanceDirectory = instanceDirectory;
        Manifest = manifest;
    }

    /// <summary>
    /// Absolute path of the instance directory.
    /// </summary>
    public string InstanceDirectory { get; }

    public EmpgManifest Manifest { get; }

    /// <summary>
    /// Absolute path of the artifact cache.
    /// </summary>
    public string CacheDirectory => Path.GetFullPath(Path.Combine(InstanceDirectory, Manifest.CacheDirectory));

    public void Save() => Manifest.Save(InstanceDirectory);

    /// <summary>
    /// Resolves a cluster by id, defaulting to the only cluster when the instance has exactly one
    /// and no id was supplied.
    /// </summary>
    /// <exception cref="EmpgException">
    /// Thrown when the id does not match a cluster, or when no id was given and the instance has
    /// more than one cluster to choose from.
    /// </exception>
    public EmpgClusterEntry ResolveCluster(string? clusterId)
    {
        if (!string.IsNullOrWhiteSpace(clusterId))
        {
            return Manifest.FindCluster(clusterId)
                ?? throw new EmpgException($"No cluster named '{clusterId}'. Run `empg cluster list` to see what exists.");
        }

        if (Manifest.Clusters.Count == 0)
        {
            throw new EmpgException("This instance has no clusters. Create one with `empg cluster add <name>`.");
        }

        if (Manifest.Clusters.Count > 1)
        {
            var names = string.Join(", ", Manifest.Clusters.Select(c => c.Id));
            throw new EmpgException($"This instance has multiple clusters ({names}). Specify one with --cluster.");
        }

        return Manifest.Clusters[0];
    }

    /// <summary>
    /// Absolute path of a cluster's data directory. Entries may hold an absolute path already,
    /// which is how cluster data lives outside a managed installation.
    /// </summary>
    public string GetDataDirectory(EmpgClusterEntry entry)
        => Path.GetFullPath(Path.Combine(InstanceDirectory, entry.DataDirectory));

    /// <summary>
    /// Absolute path of a file inside a cluster's data directory, such as pg_hba.conf.
    /// </summary>
    public string GetClusterFile(EmpgClusterEntry entry, string fileName)
        => Path.Combine(GetDataDirectory(entry), fileName);

    /// <summary>
    /// Converts a manifest entry into the library's cluster configuration.
    /// </summary>
    public PgDataClusterConfiguration ToClusterConfiguration(EmpgClusterEntry entry)
    {
        var configuration = new PgDataClusterConfiguration
        {
            UniqueId = entry.Id,
            DataDirectory = entry.DataDirectory,
            Host = entry.Host,
            Port = entry.Port,
            Superuser = entry.Superuser,
            Encoding = entry.Encoding,
            Locale = entry.Locale,
        };

        // Derived settings go in first so that anything the user set explicitly through
        // `empg config set` replaces them rather than the other way round.
        if (entry.Durable)
        {
            // Counters the library's hardcoded -F for clusters that hold real data.
            configuration.Parameters["fsync"] = "on";
            configuration.Parameters["synchronous_commit"] = "on";
            configuration.Parameters["full_page_writes"] = "on";
        }

        if (!string.IsNullOrWhiteSpace(entry.ListenAddresses))
        {
            configuration.Parameters["listen_addresses"] = entry.ListenAddresses;
        }

        foreach (var parameter in entry.Parameters)
        {
            configuration.Parameters[parameter.Key] = parameter.Value;
        }

        return configuration;
    }
}
