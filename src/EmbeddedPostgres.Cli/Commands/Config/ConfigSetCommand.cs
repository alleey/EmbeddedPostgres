using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Config;

/// <summary>
/// Sets a postgresql.conf parameter for a cluster.
/// </summary>
[Command("config set", Description = "Set a PostgreSQL parameter for a cluster.")]
public partial class ConfigSetCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ConfigSetCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "key", Description = "Parameter name, for example shared_buffers.")]
    public required string Key { get; set; }

    [CommandParameter(1, Name = "value", Description = "Parameter value.")]
    public required string Value { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to configure. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);

        entry.Parameters[Key] = Value;
        context.Save();

        await output.JsonAsync(new { cluster = entry.Id, key = Key, value = Value }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: {Key} = {Value}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg reload` to apply, or `empg restart` if the parameter needs a restart.").ConfigureAwait(false);
    }
}
