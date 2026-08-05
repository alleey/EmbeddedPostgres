using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Tears down an instance: stops its clusters and deletes their data, and with <c>--purge</c>
/// removes the PostgreSQL installation as well.
/// </summary>
/// <remarks>
/// Removing the installation is opt-in. Whether empg installed the binaries is recorded in the
/// manifest, and a manifest can be hand-edited, restored from a backup or written by an older
/// release that predates that field — so it is not something to stake an irreversible delete on.
/// The default therefore destroys only what empg can always safely identify as its own: cluster
/// data and its own state directory.
/// </remarks>
[Command("instance destroy", Description = "Stop all clusters and delete their data. Use --purge to also delete the PostgreSQL installation.")]
public partial class InstanceDestroyCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;
    private readonly PgServerBuilder serverBuilder;

    public InstanceDestroyCommand(
        IEmpgContextResolver contextResolver,
        IEmpgServerFactory serverFactory,
        PgServerBuilder serverBuilder)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
        this.serverBuilder = serverBuilder;
    }

    [CommandParameter(0, Name = "name", Description = "Instance to destroy. Defaults to the selected instance.")]
    public string? Name { get; set; }

    [CommandOption("purge", Description = "Also delete the PostgreSQL installation directory, not just cluster data and empg state.")]
    public bool Purge { get; set; }

    [CommandOption("mode", 'm', Description = "Shutdown mode: Smart, Fast or Immediate.")]
    public PgShutdownParams.ShutdownMode Mode { get; set; } = PgShutdownParams.ShutdownMode.Fast;

    [CommandOption("force", 'f', Description = "Skip the confirmation prompt.")]
    public bool Force { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        // A name given here selects the instance, the same as --instance would.
        var context = contextResolver.Resolve(Name is null ? Selector : Selector with { Name = Name });

        if (Purge && context.Manifest.Adopted)
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} is an adopted instance: empg did not install its PostgreSQL binaries, so --purge is refused. " +
                "Run `empg instance destroy` without --purge to remove the cluster data and empg state, then delete the directory yourself if you intend to.");
        }

        if (!Force && !await ConfirmAsync(console, context).ConfigureAwait(false))
        {
            await output.InfoAsync("Aborted.").ConfigureAwait(false);
            return;
        }

        var shutdownParams = PgShutdownParams.Default with { Mode = Mode };

        if (Purge)
        {
            await PurgeAsync(context, output, shutdownParams, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await DestroyDataAsync(context, output, shutdownParams, cancellationToken).ConfigureAwait(false);
        }

        // Both paths remove the manifest, so any registration pointing here is now dangling.
        await DeregisterAsync(context.InstanceDirectory, output).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops any registry entry pointing at a directory that is no longer an instance.
    /// </summary>
    private static async Task DeregisterAsync(string instanceDirectory, OutputWriter output)
    {
        var registry = EmpgRegistry.Load();

        var stale = registry.Instances
            .Where(i => string.Equals(Path.GetFullPath(i.Path), instanceDirectory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var entry in stale)
        {
            registry.Instances.Remove(entry);

            if (string.Equals(registry.Active, entry.Name, StringComparison.OrdinalIgnoreCase))
            {
                registry.Active = registry.Instances.Count == 1 ? registry.Instances[0].Name : null;
            }
        }

        registry.Save();

        await output.InfoAsync(
            $"Deregistered {string.Join(", ", stale.Select(s => $"'{s.Name}'"))}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the clusters, deletes their data directories and removes empg's state, leaving the
    /// PostgreSQL installation in place.
    /// </summary>
    private async Task DestroyDataAsync(
        EmpgContext context,
        OutputWriter output,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken)
    {
        var server = await serverFactory.OpenAsync(context, cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var cluster in server.DataClusters)
        {
            await cluster.DestroyAsync(shutdownParams, cancellationToken).ConfigureAwait(false);
        }

        var stateDirectory = Path.Combine(context.InstanceDirectory, EmpgManifest.DirectoryName);
        if (System.IO.Directory.Exists(stateDirectory))
        {
            System.IO.Directory.Delete(stateDirectory, recursive: true);
        }

        await output.JsonAsync(new
        {
            instance = context.InstanceDirectory,
            clustersDestroyed = server.DataClusters.Count,
            installationRemoved = false,
        }).ConfigureAwait(false);

        await output.SuccessAsync($"Removed cluster data and empg state from {context.InstanceDirectory}").ConfigureAwait(false);
        await output.InfoAsync("The PostgreSQL installation was left in place. Use --purge to remove it as well.").ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the clusters and deletes the whole instance directory, binaries included.
    /// </summary>
    private async Task PurgeAsync(
        EmpgContext context,
        OutputWriter output,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken)
    {
        var options = new PgServerBuilderOptions
        {
            InstanceDirectory = context.InstanceDirectory,
            CacheDirectory = context.CacheDirectory,
        };

        foreach (var entry in context.Manifest.Clusters)
        {
            options.DataClusters.Add(new PgDataClusterBuilderOptions
            {
                Configuration = context.ToClusterConfiguration(entry),
            });
        }

        StepOutOfInstance(context.InstanceDirectory);

        await serverBuilder.DestroyAsync(options, shutdownParams, cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new
        {
            instance = context.InstanceDirectory,
            clustersDestroyed = context.Manifest.Clusters.Count,
            installationRemoved = true,
        }).ConfigureAwait(false);

        await output.SuccessAsync($"Purged instance at {context.InstanceDirectory}").ConfigureAwait(false);
    }

    /// <summary>
    /// Moves this process out of the instance before it is deleted.
    /// </summary>
    /// <remarks>
    /// Windows refuses to remove a directory that is a running process's working directory, and
    /// `empg instance destroy --purge` is normally run from inside the instance being removed. Without
    /// this the contents are deleted but the root cannot be, leaving a half-destroyed instance.
    /// </remarks>
    private static void StepOutOfInstance(string instanceDirectory)
    {
        var current = Path.GetFullPath(System.IO.Directory.GetCurrentDirectory());
        var instance = Path.GetFullPath(instanceDirectory);

        var isInside = current.Equals(instance, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(instance + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (!isInside)
        {
            return;
        }

        var parent = System.IO.Directory.GetParent(instance);
        if (parent is not null && parent.Exists)
        {
            System.IO.Directory.SetCurrentDirectory(parent.FullName);
        }
    }

    private async Task<bool> ConfirmAsync(IConsole console, EmpgContext context)
    {
        var clusters = context.Manifest.Clusters.Count;

        var scope = Purge
            ? $"permanently delete {context.InstanceDirectory}, including the PostgreSQL installation and all data"
            : $"permanently delete their data and empg's state under {context.InstanceDirectory}, leaving the PostgreSQL installation in place";

        await console.Output.WriteLineAsync($"This will stop {clusters} cluster(s) and {scope}.").ConfigureAwait(false);
        await console.Output.WriteAsync("Type 'yes' to continue: ").ConfigureAwait(false);

        var answer = await console.Input.ReadLineAsync().ConfigureAwait(false);
        return string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }
}
