using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Extensions;

public static class PgServerExtensions
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
}