using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Context;

public interface IEmpgServerFactory
{
    /// <summary>
    /// Opens the instance described by the context, optionally restricted to a single cluster.
    /// </summary>
    Task<PgServer> OpenAsync(EmpgContext context, string? clusterId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds a <see cref="PgServer"/> over an already-installed instance.
/// </summary>
/// <remarks>
/// This deliberately goes through <see cref="IPgEnvironmentBuilder"/> rather than
/// <see cref="PgServerBuilder"/>: the latter treats a failed validation as a cue to download and
/// reinstall the binaries, which is correct for <c>init</c> but not for commands that are only
/// meant to inspect or control what is already there.
/// </remarks>
public class EmpgServerFactory : IEmpgServerFactory
{
    private readonly IPgEnvironmentBuilder environmentBuilder;

    public EmpgServerFactory(IPgEnvironmentBuilder environmentBuilder)
    {
        this.environmentBuilder = environmentBuilder;
    }

    public async Task<PgServer> OpenAsync(EmpgContext context, string? clusterId = null, CancellationToken cancellationToken = default)
    {
        var environment = await environmentBuilder
            .BuildAsync(PgInstanceConfiguration.NamedInstance(context.InstanceDirectory), cancellationToken)
            .ConfigureAwait(false);

        var clusters = clusterId is null
            ? context.Manifest.Clusters
            : [context.ResolveCluster(clusterId)];

        foreach (var entry in clusters)
        {
            environment.DataClusters.Add(context.ToClusterConfiguration(entry));
        }

        return new PgServer(environment);
    }
}
