using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Chooses the instance commands act on when nothing else selects one.
/// </summary>
[Command("instance use", Description = "Make an instance the active one.")]
public partial class InstanceUseCommand : EmpgCommandBase
{
    [CommandParameter(0, Name = "name", Description = "Instance to activate.")]
    public required string Name { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var registry = EmpgRegistry.Load();

        var entry = registry.Find(Name)
            ?? throw new EmpgException(
                $"No instance named '{Name}'. " +
                (registry.Instances.Count == 0
                    ? "None are registered yet."
                    : $"Registered: {string.Join(", ", registry.Instances.Select(i => i.Name))}."));

        registry.Active = entry.Name;
        registry.Save();

        await output.JsonAsync(new { active = entry.Name, path = entry.Path }).ConfigureAwait(false);
        await output.SuccessAsync($"Active instance is now '{entry.Name}' ({entry.Path})").ConfigureAwait(false);

        // Standing inside a different instance still wins, so say so rather than let it surprise.
        await output.InfoAsync(
            "Commands run from inside another instance's directory still act on that one.").ConfigureAwait(false);
    }
}
