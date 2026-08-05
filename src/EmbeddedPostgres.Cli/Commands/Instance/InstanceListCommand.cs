using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Lists the registered instances, marking the active one.
/// </summary>
[Command("instance list", Description = "List registered instances.")]
public partial class InstanceListCommand : EmpgCommandBase
{
    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var registry = EmpgRegistry.Load();

        var rows = registry.Instances
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new
            {
                i.Name,
                i.Path,
                Active = string.Equals(i.Name, registry.Active, StringComparison.OrdinalIgnoreCase),
                // A registration outlives the directory it points at, so say so rather than
                // failing only once a command tries to use it.
                Present = File.Exists(EmpgManifest.GetManifestPath(i.Path)),
                Kind = ReadKind(i.Path),
            })
            .ToList();

        await output.JsonAsync(new { active = registry.Active, instances = rows }).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            await output.LineAsync(
                "No instances registered. Create one with `empg instance create`, adopt an existing installation with "
                + "`empg instance adopt <path>`, or register an existing instance with `empg instance add <name> <path>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["", "NAME", "KIND", "PATH", "STATE"],
            rows.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Active ? "*" : " ",
                r.Name,
                r.Kind ?? "-",
                r.Path,
                r.Present ? "ok" : "missing",
            }).ToList()).ConfigureAwait(false);

        if (rows.Any(r => !r.Present))
        {
            await output.WarnAsync(
                "Instances marked 'missing' no longer exist at their registered path. " +
                "Drop them with `empg instance remove <name>`.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads whether an instance is managed or adopted, without failing the listing when its
    /// manifest is absent or unreadable.
    /// </summary>
    private static string? ReadKind(string path)
    {
        try
        {
            return EmpgManifest.Load(path).Kind;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
