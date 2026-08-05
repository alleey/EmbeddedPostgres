using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Reports where the instance is and what each of its clusters is doing.
/// </summary>
[Command("status", Description = "Show the instance and the state of its clusters.")]
public partial class StatusCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public StatusCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Limit output to a single cluster.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var server = await serverFactory.OpenAsync(context, Cluster, cancellationToken).ConfigureAwait(false);

        var report = new List<ClusterStatus>();
        foreach (var cluster in server.DataClusters)
        {
            var runtime = await cluster.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            report.Add(new ClusterStatus(
                cluster.UniqueId,
                cluster.IsInitialized(),
                runtime.IsValid,
                runtime.IsValid ? runtime.Pid : null,
                cluster.Settings.Host,
                // A stopped cluster has no runtime port, so fall back to its configured one.
                runtime.IsValid ? runtime.Port : cluster.Settings.Port,
                cluster.Settings.Superuser));
        }

        await output.JsonAsync(new
        {
            instance = context.InstanceDirectory,
            postgresVersion = context.Manifest.PostgresVersion,
            artifact = context.Manifest.ServerArtifact,
            extensions = context.Manifest.Extensions,
            clusters = report,
        }).ConfigureAwait(false);

        await output.LineAsync($"Instance:  {context.InstanceDirectory}").ConfigureAwait(false);
        await output.LineAsync($"Postgres:  {context.Manifest.PostgresVersion ?? "unknown"}").ConfigureAwait(false);

        if (context.Manifest.Extensions.Count > 0)
        {
            await output.LineAsync($"Extensions: {context.Manifest.Extensions.Count}").ConfigureAwait(false);
        }

        await output.LineAsync(string.Empty).ConfigureAwait(false);

        if (report.Count == 0)
        {
            await output.LineAsync("No clusters. Create one with `empg cluster add <name>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["CLUSTER", "STATE", "PID", "HOST", "PORT", "SUPERUSER"],
            report.Select(c => (IReadOnlyList<string>)new[]
            {
                c.Id,
                Describe(c),
                c.Pid?.ToString() ?? "-",
                c.Host,
                c.Port.ToString(),
                c.Superuser,
            }).ToList()).ConfigureAwait(false);
    }

    private static string Describe(ClusterStatus status)
    {
        if (!status.Initialized)
        {
            return "uninitialized";
        }
        return status.Running ? "running" : "stopped";
    }

    private sealed record ClusterStatus(
        string Id,
        bool Initialized,
        bool Running,
        int? Pid,
        string Host,
        int Port,
        string Superuser);
}
