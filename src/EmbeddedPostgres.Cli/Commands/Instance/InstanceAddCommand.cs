using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Registers an existing instance under a name.
/// </summary>
[Command("instance add", Description = "Register an existing instance under a name.")]
public partial class InstanceAddCommand : EmpgCommandBase
{
    [CommandParameter(0, Name = "name", Description = "Name to refer to the instance by.")]
    public required string Name { get; set; }

    [CommandParameter(1, Name = "path", Description = "Directory holding the instance.")]
    public required string InstancePath { get; set; }

    [CommandOption("use", Description = "Also make this the active instance.")]
    public bool Use { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var name = InstanceName.Require(Name);
        var path = Path.GetFullPath(InstancePath);

        if (!File.Exists(EmpgManifest.GetManifestPath(path)))
        {
            throw new EmpgException(
                $"{path} is not an empg instance. Create one with `empg instance create {path}`, " +
                $"or adopt binaries already there with `empg instance adopt {path}`.");
        }

        var registry = EmpgRegistry.Load();

        if (registry.Find(name) is { } existing)
        {
            throw new EmpgException(
                $"An instance named '{name}' is already registered at {existing.Path}. " +
                $"Remove it first with `empg instance remove {name}`.");
        }

        registry.Instances.Add(new EmpgRegistryEntry { Name = name, Path = path });

        // The first instance registered becomes the default, so a single-instance setup needs no
        // further ceremony.
        var activated = Use || string.IsNullOrWhiteSpace(registry.Active);
        if (activated)
        {
            registry.Active = name;
        }

        registry.Save();

        await output.JsonAsync(new { name, path, active = activated }).ConfigureAwait(false);
        await output.SuccessAsync($"Registered '{name}' at {path}").ConfigureAwait(false);

        if (activated)
        {
            await output.InfoAsync($"'{name}' is now the active instance.").ConfigureAwait(false);
        }
    }
}
