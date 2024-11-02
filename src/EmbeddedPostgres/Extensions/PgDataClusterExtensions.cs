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
        => Helpers.WaitForServerStartup(server.Settings.Host, server.Settings.Port);

    public static void WaitForStartup(this PgDataClusterConfiguration config, int waitTimeoutMs = 30000)
        => Helpers.WaitForServerStartup(config.Host, config.Port);

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