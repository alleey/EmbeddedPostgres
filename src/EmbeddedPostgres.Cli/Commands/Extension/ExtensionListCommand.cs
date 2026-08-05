using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Extension;

/// <summary>
/// Lists the extensions installed into the instance.
/// </summary>
[Command("extension list", Description = "List extensions installed into the instance.")]
public partial class ExtensionListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ExtensionListCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var extensions = context.Manifest.Extensions;

        await output.JsonAsync(new { instance = context.InstanceDirectory, extensions }).ConfigureAwait(false);

        if (extensions.Count == 0)
        {
            await output.LineAsync("No extensions installed. Add one with `empg extension add <url|path>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["SOURCE"],
            extensions.Select(e => (IReadOnlyList<string>)new[] { e }).ToList()).ConfigureAwait(false);
    }
}
