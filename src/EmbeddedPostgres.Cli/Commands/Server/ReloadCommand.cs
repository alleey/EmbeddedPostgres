using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Server;

/// <summary>
/// Asks running clusters to re-read their configuration without restarting.
/// </summary>
[Command("reload", Description = "Reload cluster configuration without restarting.")]
public partial class ReloadCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public ReloadCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to reload. Defaults to all clusters.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var server = await serverFactory.OpenAsync(context, Cluster, cancellationToken).ConfigureAwait(false);

        var results = new List<ClusterResult>();

        await server.ReloadConfigurationAsync(
            eventListener: async (evt, ct) =>
            {
                results.Add(new ClusterResult(evt.DataCluster.UniqueId, evt.IsSuccess, evt.ErrorInfo?.Message));
                if (evt.IsSuccess)
                {
                    await output.SuccessAsync($"Reloaded {evt.DataCluster.UniqueId}").ConfigureAwait(false);
                }
                else
                {
                    await output.WarnAsync($"Failed to reload {evt.DataCluster.UniqueId}: {evt.ErrorInfo?.Message}").ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { instance = context.InstanceDirectory, clusters = results }).ConfigureAwait(false);

        if (results.Any(r => !r.Reloaded))
        {
            throw new EmpgException("One or more clusters failed to reload.");
        }
    }

    private sealed record ClusterResult(string Id, bool Reloaded, string? Error);
}
