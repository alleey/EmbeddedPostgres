using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Config;

/// <summary>
/// Prints a single configured parameter.
/// </summary>
[Command("config get", Description = "Print a PostgreSQL parameter for a cluster.")]
public partial class ConfigGetCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ConfigGetCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "key", Description = "Parameter name.")]
    public required string Key { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to read from. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);

        if (!entry.Parameters.TryGetValue(Key, out var value))
        {
            // Mirrors `git config` on a missing key: no output, non-zero exit.
            throw new EmpgException($"'{Key}' is not set for cluster '{entry.Id}'.");
        }

        await output.JsonAsync(new { cluster = entry.Id, key = Key, value }).ConfigureAwait(false);
        await output.LineAsync(value).ConfigureAwait(false);
    }
}
