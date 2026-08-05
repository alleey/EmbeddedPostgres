using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Shows which instance a command would act on, and why.
/// </summary>
[Command("instance show", Description = "Show the instance commands would act on.")]
public partial class InstanceShowCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public InstanceShowCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var registry = EmpgRegistry.Load();

        // Report the same order the resolver applies, so the answer explains itself.
        var reason = Instance is not null ? $"--instance {Instance}"
            : Directory is not null ? $"-C {Directory}"
            : Environment.GetEnvironmentVariable(EmpgContextResolver.DirectoryVariable) is not null ? EmpgContextResolver.DirectoryVariable
            : IsWithin(context.InstanceDirectory) ? "the working directory"
            : $"the active instance '{registry.Active}'";

        var registered = registry.Instances.FirstOrDefault(i =>
            string.Equals(Path.GetFullPath(i.Path), context.InstanceDirectory, StringComparison.OrdinalIgnoreCase));

        await output.JsonAsync(new
        {
            name = registered?.Name,
            path = context.InstanceDirectory,
            selectedBy = reason,
            kind = context.Manifest.Kind,
            postgresVersion = context.Manifest.PostgresVersion,
            clusters = context.Manifest.Clusters.Select(c => c.Id),
        }).ConfigureAwait(false);

        await output.LineAsync($"Name:      {registered?.Name ?? "(unregistered)"}").ConfigureAwait(false);
        await output.LineAsync($"Path:      {context.InstanceDirectory}").ConfigureAwait(false);
        await output.LineAsync($"Selected:  {reason}").ConfigureAwait(false);
        await output.LineAsync($"Postgres:  {context.Manifest.PostgresVersion ?? "unknown"}").ConfigureAwait(false);
        await output.LineAsync(
            $"Kind:      {context.Manifest.Kind}"
            + (context.Manifest.Adopted
                ? " (empg did not install these binaries and will not delete them)"
                : " (empg installed these binaries)")).ConfigureAwait(false);
        await output.LineAsync($"Clusters:  {(context.Manifest.Clusters.Count == 0 ? "none" : string.Join(", ", context.Manifest.Clusters.Select(c => c.Id)))}").ConfigureAwait(false);

        if (registered is null)
        {
            await output.InfoAsync(
                $"This instance is not registered. Give it a name with `empg instance add <name> {context.InstanceDirectory}`.").ConfigureAwait(false);
        }
    }

    private static bool IsWithin(string instanceDirectory)
    {
        var current = Path.GetFullPath(System.IO.Directory.GetCurrentDirectory());
        return current.Equals(instanceDirectory, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(instanceDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
