using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Cluster;

/// <summary>
/// Stops a cluster, removes it from the manifest and optionally deletes its data.
/// </summary>
[Command("cluster remove", Description = "Remove a data cluster from the instance.")]
public partial class ClusterRemoveCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public ClusterRemoveCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "name", Description = "Name of the cluster to remove.")]
    public required string Name { get; set; }

    [CommandOption("keep-data", Description = "Deregister the cluster but leave its data directory on disk.")]
    public bool KeepData { get; set; }

    [CommandOption("force", 'f', Description = "Skip the confirmation prompt.")]
    public bool Force { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);

        var entry = context.Manifest.FindCluster(Name)
            ?? throw new EmpgException($"No cluster named '{Name}'. Run `empg cluster list` to see what exists.");

        if (!KeepData && !Force && !await ConfirmAsync(console, context, entry).ConfigureAwait(false))
        {
            await output.InfoAsync("Aborted.").ConfigureAwait(false);
            return;
        }

        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        if (KeepData)
        {
            await cluster.StopAsync(PgShutdownParams.Fast, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // DestroyAsync stops the cluster and deletes its data directory.
            await cluster.DestroyAsync(PgShutdownParams.Fast, cancellationToken).ConfigureAwait(false);
        }

        context.Manifest.Clusters.Remove(entry);
        context.Save();

        await output.JsonAsync(new { removed = entry.Id, dataDeleted = !KeepData }).ConfigureAwait(false);
        await output.SuccessAsync($"Removed cluster '{entry.Id}'").ConfigureAwait(false);
    }

    private static async Task<bool> ConfirmAsync(IConsole console, EmpgContext context, EmpgClusterEntry entry)
    {
        var dataPath = Path.GetFullPath(Path.Combine(context.InstanceDirectory, entry.DataDirectory));
        await console.Output.WriteLineAsync($"This will stop cluster '{entry.Id}' and permanently delete {dataPath}.").ConfigureAwait(false);
        await console.Output.WriteAsync("Type 'yes' to continue: ").ConfigureAwait(false);

        var answer = await console.Input.ReadLineAsync().ConfigureAwait(false);
        return string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }
}
