using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Config;

/// <summary>
/// Removes a configured parameter, returning it to the PostgreSQL default.
/// </summary>
[Command("config unset", Description = "Remove a PostgreSQL parameter from a cluster.")]
public partial class ConfigUnsetCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ConfigUnsetCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "key", Description = "Parameter name.")]
    public required string Key { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to modify. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);

        if (!entry.Parameters.Remove(Key))
        {
            throw new EmpgException($"'{Key}' is not set for cluster '{entry.Id}'.");
        }

        context.Save();

        await output.JsonAsync(new { cluster = entry.Id, key = Key, removed = true }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: unset {Key}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg restart` to apply.").ConfigureAwait(false);
    }
}
