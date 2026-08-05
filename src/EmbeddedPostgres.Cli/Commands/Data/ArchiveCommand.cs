using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Compresses a cluster's data directory into a single archive.
/// </summary>
/// <remarks>
/// This is a cold backup: the underlying operation stops the cluster before compressing, and it
/// is not restarted afterwards.
/// </remarks>
[Command("archive", Description = "Stop a cluster and archive its data directory.")]
public partial class ArchiveCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public ArchiveCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "target", Description = "Archive file to write.")]
    public required string Target { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to archive. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    [CommandOption("mode", 'm', Description = "Shutdown mode used before archiving: Smart, Fast or Immediate.")]
    public PgShutdownParams.ShutdownMode Mode { get; set; } = PgShutdownParams.ShutdownMode.Fast;

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        await output.InfoAsync($"Stopping '{entry.Id}' and archiving to {Target} ...").ConfigureAwait(false);

        await cluster.ArchiveAsync(
            Target,
            PgShutdownParams.Default with { Mode = Mode },
            cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, target = Target, clusterStopped = true }).ConfigureAwait(false);
        await output.SuccessAsync($"Archived '{entry.Id}' to {Target}").ConfigureAwait(false);
        await output.InfoAsync($"Cluster '{entry.Id}' is stopped. Run `empg start` to bring it back up.").ConfigureAwait(false);
    }
}
