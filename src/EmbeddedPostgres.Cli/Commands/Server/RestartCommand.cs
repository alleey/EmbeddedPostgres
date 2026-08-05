using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Server;

/// <summary>
/// Stops and starts clusters in one step.
/// </summary>
[Command("restart", Description = "Restart the instance's clusters.")]
public partial class RestartCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public RestartCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to restart. Defaults to all clusters.")]
    public string? Cluster { get; set; }

    [CommandOption("mode", 'm', Description = "Shutdown mode used for the stop half: Smart, Fast or Immediate.")]
    public PgShutdownParams.ShutdownMode Mode { get; set; } = PgShutdownParams.ShutdownMode.Fast;

    [CommandOption("timeout", 't', Description = "Seconds to wait for startup.")]
    public int TimeoutSecs { get; set; } = 30;

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var server = await serverFactory.OpenAsync(context, Cluster, cancellationToken).ConfigureAwait(false);

        await server.StopAsync(
            shutdownParams: PgShutdownParams.Default with { Mode = Mode, Wait = true },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.InfoAsync("Stopped. Starting ...").ConfigureAwait(false);

        var results = new List<ClusterResult>();

        await server.StartAsync(
            startupParams: PgStartupParams.Default with { Wait = true, WaitTimeoutSecs = TimeoutSecs },
            initializer: _ => PgClusterInitializerFactory.FromEnvironment(server.Environment).InitializeUsingInitDb(),
            eventListener: async (evt, ct) =>
            {
                results.Add(new ClusterResult(evt.DataCluster.UniqueId, evt.IsSuccess, evt.ErrorInfo?.Message));
                if (evt.IsSuccess)
                {
                    await output.SuccessAsync($"Restarted {evt.DataCluster.UniqueId} on {evt.DataCluster.Settings.Host}:{evt.DataCluster.Settings.Port}").ConfigureAwait(false);
                }
                else
                {
                    await output.WarnAsync($"Failed to restart {evt.DataCluster.UniqueId}: {evt.ErrorInfo?.Message}").ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { instance = context.InstanceDirectory, clusters = results }).ConfigureAwait(false);

        if (results.Any(r => !r.Restarted))
        {
            throw new EmpgException("One or more clusters failed to restart.");
        }
    }

    private sealed record ClusterResult(string Id, bool Restarted, string? Error);
}
