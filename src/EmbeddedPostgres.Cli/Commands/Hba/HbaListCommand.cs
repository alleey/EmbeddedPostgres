using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Shows the client authentication records for a cluster, in the order PostgreSQL evaluates them.
/// </summary>
[Command("hba list", Description = "List a cluster's client authentication rules.")]
public partial class HbaListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public HbaListCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to inspect. Required when the instance has more than one.")]
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

        var rules = PgHbaFile.Read(await File.ReadAllTextAsync(path).ConfigureAwait(false));

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            file = path,
            rules = rules.Select(r => new
            {
                r.Rule.Type,
                r.Rule.Database,
                r.Rule.User,
                r.Rule.Address,
                r.Rule.Method,
                r.Rule.Options,
                r.Managed,
            }),
        }).ConfigureAwait(false);

        await output.TableAsync(
            ["#", "TYPE", "DATABASE", "USER", "ADDRESS", "METHOD", "OWNER"],
            rules.Select((r, index) => (IReadOnlyList<string>)new[]
            {
                (index + 1).ToString(),
                r.Rule.Type,
                r.Rule.Database,
                r.Rule.User,
                string.IsNullOrEmpty(r.Rule.Address) ? "-" : r.Rule.Address,
                r.Rule.Method,
                r.Managed ? "empg" : "manual",
            }).ToList()).ConfigureAwait(false);

        // First match wins, so a permissive record early in the file silently defeats later ones.
        await output.InfoAsync(string.Empty).ConfigureAwait(false);
        await output.InfoAsync("Rules are evaluated top to bottom; the first match decides.").ConfigureAwait(false);

        var trust = rules.Where(r => r.Rule.Method.Equals("trust", StringComparison.OrdinalIgnoreCase)).ToList();
        if (trust.Count > 0)
        {
            await output.WarnAsync(
                $"{trust.Count} rule(s) still use 'trust', which accepts any connection claiming the role without a password. " +
                "Run `empg hba harden` once the accounts have passwords.").ConfigureAwait(false);
        }
    }
}
