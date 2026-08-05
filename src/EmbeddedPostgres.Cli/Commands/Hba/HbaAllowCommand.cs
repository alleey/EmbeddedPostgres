using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Adds a client authentication rule to the managed block.
/// </summary>
[Command("hba allow", Description = "Permit connections from an address range.")]
public partial class HbaAllowCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public HbaAllowCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "address", Description = "CIDR such as 192.168.1.0/24, or a keyword such as samenet.")]
    public required string Address { get; set; }

    [CommandOption("method", 'm', Description = "Authentication method: scram-sha-256, md5, sspi, cert, reject.")]
    public string Method { get; set; } = "scram-sha-256";

    [CommandOption("user", 'u', Description = "Role the rule applies to.")]
    public string User { get; set; } = "all";

    [CommandOption("database", 'd', Description = "Database the rule applies to.")]
    public string Database { get; set; } = "all";

    [CommandOption("type", 't', Description = "Connection type: host, hostssl or hostnossl.")]
    public string Type { get; set; } = "host";

    [CommandOption("map", Description = "Ident map name, for use with the sspi or ident methods.")]
    public string? Map { get; set; }

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
            Method = Method,
            Options = string.IsNullOrWhiteSpace(Map) ? string.Empty : $"map={Map}",
        };

        // 'trust' authenticates nobody: it accepts whatever role name the client claims. Reachable
        // from the network that is an open door, so it is confined to loopback.
        if (Method.Equals("trust", StringComparison.OrdinalIgnoreCase) && !rule.IsLoopback)
        {
            throw new EmpgException(
                $"Refusing to grant 'trust' to {Address}: it accepts any client claiming a role, with no password. " +
                "Use --method scram-sha-256 and give the role a password with `empg role password`.");
        }

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var updated = PgHbaFile.Allow(text, rule);

        if (string.Equals(updated, text, StringComparison.Ordinal))
        {
            await output.InfoAsync("Rule already present; nothing changed.").ConfigureAwait(false);
            return;
        }

        await File.WriteAllTextAsync(path, updated).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, rule = rule.ToLine(), file = path }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: {rule.ToLine()}").ConfigureAwait(false);

        // pg_hba is re-read on SIGHUP, so a reload is enough — no restart needed.
        await output.InfoAsync("Run `empg reload` to apply.").ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(entry.ListenAddresses) && !rule.IsLoopback)
        {
            await output.WarnAsync(
                $"Cluster '{entry.Id}' still binds to loopback only, so this rule cannot match yet. " +
                "Set the bind address with `empg listen <addresses>` and restart.").ConfigureAwait(false);
        }
    }
}
