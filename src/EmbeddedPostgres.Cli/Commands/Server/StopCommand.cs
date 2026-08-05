using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Server;

/// <summary>
/// Stops one or all clusters.
/// </summary>
[Command("stop", Description = "Stop the instance's clusters.")]
public partial class StopCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public StopCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to stop. Defaults to all clusters.")]
    public string? Cluster { get; set; }

    [CommandOption("mode", 'm', Description = "Shutdown mode: Smart, Fast or Immediate.")]
    public PgShutdownParams.ShutdownMode Mode { get; set; } = PgShutdownParams.ShutdownMode.Smart;

    [CommandOption("wait", 'w', Description = "Wait for shutdown to complete.")]
    public bool Wait { get; set; } = true;

    [CommandOption("timeout", 't', Description = "Seconds to wait for shutdown.")]
    public int TimeoutSecs { get; set; } = 180;

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var server = await serverFactory.OpenAsync(context, Cluster, cancellationToken).ConfigureAwait(false);

        var results = new List<ClusterResult>();

        await server.StopAsync(
            shutdownParams: PgShutdownParams.Default with { Mode = Mode, Wait = Wait, WaitTimeoutSecs = TimeoutSecs },
            eventListener: async (evt, ct) =>
            {
                results.Add(new ClusterResult(evt.DataCluster.UniqueId, evt.IsSuccess, evt.ErrorInfo?.Message));
                if (evt.IsSuccess)
                {
                    await output.SuccessAsync($"Stopped {evt.DataCluster.UniqueId}").ConfigureAwait(false);
                }
                else
                {
                    await output.WarnAsync($"Failed to stop {evt.DataCluster.UniqueId}: {evt.ErrorInfo?.Message}").ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { instance = context.InstanceDirectory, clusters = results }).ConfigureAwait(false);

        if (results.Any(r => !r.Stopped))
        {
            throw new EmpgException("One or more clusters failed to stop.");
        }
    }

    private sealed record ClusterResult(string Id, bool Stopped, string? Error);
}
