using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Drops an instance's registration.
/// </summary>
/// <remarks>
/// This forgets a name. Nothing on disk is touched — the instance, its clusters and their data all
/// remain, and the same directory can be registered again. Use <c>empg instance destroy</c> to
/// remove data.
/// </remarks>
[Command("instance remove", Description = "Forget an instance's registration. Deletes nothing on disk.")]
public partial class InstanceRemoveCommand : EmpgCommandBase
{
    [CommandParameter(0, Name = "name", Description = "Instance to deregister.")]
    public required string Name { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var registry = EmpgRegistry.Load();

        var entry = registry.Find(Name)
            ?? throw new EmpgException($"No instance named '{Name}' is registered.");

        registry.Instances.Remove(entry);

        var wasActive = string.Equals(registry.Active, entry.Name, StringComparison.OrdinalIgnoreCase);
        if (wasActive)
        {
            // Fall back to the only remaining instance when there is one; otherwise leave the
            // choice to the user rather than picking arbitrarily.
            registry.Active = registry.Instances.Count == 1 ? registry.Instances[0].Name : null;
        }

        registry.Save();

        await output.JsonAsync(new
        {
            removed = entry.Name,
            path = entry.Path,
            active = registry.Active,
            filesDeleted = false,
        }).ConfigureAwait(false);

        await output.SuccessAsync($"Deregistered '{entry.Name}'. Nothing at {entry.Path} was deleted.").ConfigureAwait(false);

        if (wasActive)
        {
            await output.InfoAsync(
                registry.Active is null
                    ? "There is no active instance now; choose one with `empg instance use <name>`."
                    : $"'{registry.Active}' is now the active instance.").ConfigureAwait(false);
        }
    }
}
