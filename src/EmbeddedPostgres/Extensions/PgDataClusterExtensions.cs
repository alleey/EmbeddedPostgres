using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Extensions;

public static class PgDataClusterExtensions
{
    public static async Task<bool> IsRunningAsync(this PgDataCluster server, CancellationToken cancellationToken = default)
    {
        var status = await server.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return status.IsValid;
    }

    public static void WaitForStartup(this PgDataCluster server, int waitTimeoutMs = 30000)
        => Helpers.WaitForServerStartup(ReadinessProbeHost(server.Settings), server.Settings.Port, waitTimeoutMs);

    public static void WaitForStartup(this PgDataClusterConfiguration config, int waitTimeoutMs = 30000)
        => Helpers.WaitForServerStartup(ReadinessProbeHost(config), config.Port, waitTimeoutMs);

    /// <summary>
    /// The address to poll when waiting for a cluster to accept connections.
    /// </summary>
    /// <remarks>
    /// <see cref="PgDataClusterConfiguration.Host"/> is where clients are told to connect, which is
    /// not necessarily where the server binds: binding is governed by <c>listen_addresses</c>.
    /// Polling the advertised host means that on a multi-homed machine the readiness check can
    /// never succeed even though the server started perfectly well, so the bind address wins when
    /// one is configured.
    /// </remarks>
    private static string ReadinessProbeHost(PgDataClusterConfiguration config)
    {
        if (!config.Parameters.TryGetValue("listen_addresses", out var configured))
        {
            // No explicit binding: PostgreSQL listens on loopback, whatever Host advertises.
            return "localhost";
        }

        var first = configured?.ToString()?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(first))
        {
            return "localhost";
        }

        // Wildcards mean "every interface", which always includes loopback.
        return first is "*" or "0.0.0.0" or "::" ? "localhost" : first;
    }

    /// <summary>
    /// Returns the full path of the instance directory specified in the <paramref name="configuration"/>.
    /// </summary>
    /// <param name="cluster">The <see cref="PgDataCluster"/> instance containing the directory details.</param>
    /// <returns>
    /// A string representing the full path to the instance directory.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetInstanceFullPath(this PgDataCluster cluster)
        => cluster.Environment.GetInstanceFullPath();

    /// <summary>
    /// Returns the full path of the data/database directory. 
    /// </summary>
    /// <returns>
    /// A string representing the full path to the data/database directory. The full path is constructed by combining
    /// the <see cref="PgInstanceConfiguration.InstanceDirectory"/> with the 
    /// <see cref="PgDataClusterConfiguration.DataDirectory"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetDataFullPath(this PgDataCluster cluster)
        => cluster.Environment.Instance.GetDataFullPath(cluster.Settings);
}