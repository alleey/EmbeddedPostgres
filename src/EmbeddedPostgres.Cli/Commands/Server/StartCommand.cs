using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Server;

/// <summary>
/// Starts one or all clusters, initialising any that have not been initialised yet.
/// </summary>
[Command("start", Description = "Start the instance's clusters.")]
public partial class StartCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public StartCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to start. Defaults to all clusters.")]
    public string? Cluster { get; set; }

    [CommandOption("wait", 'w', Description = "Wait until the server is accepting connections.")]
    public bool Wait { get; set; } = true;

    [CommandOption("timeout", 't', Description = "Seconds to wait for startup.")]
    public int TimeoutSecs { get; set; } = 30;

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var server = await serverFactory.OpenAsync(context, Cluster, cancellationToken).ConfigureAwait(false);

        var results = new List<ClusterResult>();

        await server.StartAsync(
            startupParams: PgStartupParams.Default with { Wait = Wait, WaitTimeoutSecs = TimeoutSecs },
            // Initialise on demand so `start` works on a cluster added but never initialised.
            initializer: _ => PgClusterInitializerFactory.FromEnvironment(server.Environment).InitializeUsingInitDb(),
            eventListener: async (evt, ct) =>
            {
                results.Add(new ClusterResult(evt.DataCluster.UniqueId, evt.IsSuccess, evt.ErrorInfo?.Message));
                if (evt.IsSuccess)
                {
                    await output.SuccessAsync($"Started {evt.DataCluster.UniqueId} on {evt.DataCluster.Settings.Host}:{evt.DataCluster.Settings.Port}").ConfigureAwait(false);
                }
                else
                {
                    await output.WarnAsync($"Failed to start {evt.DataCluster.UniqueId}: {evt.ErrorInfo?.Message}").ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { instance = context.InstanceDirectory, clusters = results }).ConfigureAwait(false);

        if (results.Any(r => !r.Started))
        {
            throw new EmpgException("One or more clusters failed to start.");
        }
    }

    private sealed record ClusterResult(string Id, bool Started, string? Error);
}
