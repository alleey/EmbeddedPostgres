using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Removes a rule from the managed block.
/// </summary>
[Command("hba revoke", Description = "Remove a previously allowed address range.")]
public partial class HbaRevokeCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public HbaRevokeCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "address", Description = "Address of the rule to remove.")]
    public required string Address { get; set; }

    [CommandOption("user", 'u', Description = "Role the rule applies to.")]
    public string User { get; set; } = "all";

    [CommandOption("database", 'd', Description = "Database the rule applies to.")]
    public string Database { get; set; } = "all";

    [CommandOption("type", 't', Description = "Connection type the rule uses.")]
    public string Type { get; set; } = "host";

    [CommandOption("cluster", 'c', Description = "Cluster to configure. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var path = context.GetClusterFile(entry, PgHbaFile.FileName);

        if (!File.Exists(path))
        {
            throw new EmpgException($"{path} does not exist. The cluster has not been initialised yet.");
        }

        var rule = new PgHbaRule
        {
            Type = Type,
            Database = Database,
            User = User,
            Address = Address,
        };

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var (updated, removed) = PgHbaFile.Revoke(text, rule);

        if (!removed)
        {
            // Only the managed block is ours to edit; a hand-written rule has to be removed by hand.
            throw new EmpgException(
                $"No empg-managed rule matches {Type} {Database} {User} {Address}. " +
                "Run `empg hba list` to see which rules are managed.");
        }

        await File.WriteAllTextAsync(path, updated).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, revoked = rule.ToLine(), file = path }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: revoked {Type} {Database} {User} {Address}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg reload` to apply.").ConfigureAwait(false);
    }
}
