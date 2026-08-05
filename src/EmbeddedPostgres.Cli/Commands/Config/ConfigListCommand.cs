using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Config;

/// <summary>
/// Lists the parameters configured for one or all clusters.
/// </summary>
[Command("config list", Description = "List configured PostgreSQL parameters.")]
public partial class ConfigListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ConfigListCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to list. Defaults to all clusters.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);

        var clusters = Cluster is null
            ? context.Manifest.Clusters.ToList()
            : [context.ResolveCluster(Cluster)];

        await output.JsonAsync(new
        {
            clusters = clusters.Select(c => new { c.Id, c.Parameters }),
        }).ConfigureAwait(false);

        var rows = clusters
            .SelectMany(c => c.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (IReadOnlyList<string>)new[] { c.Id, p.Key, p.Value }))
            .ToList();

        if (rows.Count == 0)
        {
            await output.LineAsync("No parameters set. Use `empg config set <key> <value>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(["CLUSTER", "KEY", "VALUE"], rows).ConfigureAwait(false);
    }
}
