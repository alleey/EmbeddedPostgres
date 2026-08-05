using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Cluster;

/// <summary>
/// Lists the clusters registered in the manifest.
/// </summary>
[Command("cluster list", Description = "List the instance's data clusters.")]
public partial class ClusterListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ClusterListCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var clusters = context.Manifest.Clusters;

        await output.JsonAsync(new { instance = context.InstanceDirectory, clusters }).ConfigureAwait(false);

        if (clusters.Count == 0)
        {
            await output.LineAsync("No clusters. Create one with `empg cluster add <name>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["CLUSTER", "DATA", "HOST", "PORT", "SUPERUSER"],
            clusters.Select(c => (IReadOnlyList<string>)new[]
            {
                c.Id,
                c.DataDirectory,
                c.Host,
                c.Port.ToString(),
                c.Superuser,
            }).ToList()).ConfigureAwait(false);
    }
}
