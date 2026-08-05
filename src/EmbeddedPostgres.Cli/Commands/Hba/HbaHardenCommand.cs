using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Replaces initdb's <c>trust</c> rules so that every connection must prove who it is.
/// </summary>
/// <remarks>
/// Ordering is the safety property here. Under <c>trust</c> a password is accepted but not
/// required, so the password can be set over a connection that works both before and after the
/// change; dropping trust first would leave no way back in. This command therefore refuses to run
/// until the account it is about to start challenging actually has a password.
/// </remarks>
[Command("hba harden", Description = "Replace trust rules with password authentication.")]
public partial class HbaHardenCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public HbaHardenCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("method", 'm', Description = "Method to replace trust with: scram-sha-256, md5 or sspi.")]
    public string Method { get; set; } = "scram-sha-256";

    [CommandOption("cluster", 'c', Description = "Cluster to harden. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var path = context.GetClusterFile(entry, PgHbaFile.FileName);

        if (!File.Exists(path))
        {
            throw new EmpgException($"{path} does not exist. The cluster has not been initialised yet.");
        }

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        if (!PgHbaFile.HasTrustRules(text))
        {
            await output.InfoAsync($"Cluster '{entry.Id}' has no trust rules left; nothing to harden.").ConfigureAwait(false);
            return;
        }

        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        if (RequiresPassword(Method))
        {
            await EnsureSuperuserHasPasswordAsync(cluster, entry, cancellationToken).ConfigureAwait(false);
        }

        var hardened = PgHbaFile.HardenDefaults(text, Method);
        await File.WriteAllTextAsync(path, hardened).ConfigureAwait(false);

        // Signal the reload through pg_ctl rather than a SQL call: once the new rules are in force
        // a client connection needs the very credentials this command has just started requiring,
        // whereas pg_ctl signals the postmaster directly using the data directory.
        await cluster.ReloadConfigurationAsync(cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, method = Method, file = path, hardened = true }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: trust rules replaced with {Method} and configuration reloaded.").ConfigureAwait(false);
        await output.InfoAsync(
            $"Connections now require a password for '{entry.Superuser}'. Set PGPASSWORD in the environment " +
            "so empg's own commands can still reach this cluster.").ConfigureAwait(false);
    }

    private static bool RequiresPassword(string method)
        => method.Equals("scram-sha-256", StringComparison.OrdinalIgnoreCase)
        || method.Equals("md5", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Refuses to continue unless the cluster superuser already has a password set.
    /// </summary>
    private async Task EnsureSuperuserHasPasswordAsync(
        PgDataCluster cluster,
        EmpgClusterEntry entry,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();

        await cluster.ExecuteSqlAsync(
            "SELECT rolpassword IS NOT NULL AS has_password FROM pg_authid WHERE rolname = current_user;",
            listener: (line, ct) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line.Trim());
                }
                return Task.CompletedTask;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Output is CSV: a header row followed by the value.
        var value = lines.LastOrDefault();
        if (string.Equals(value, "t", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new EmpgException(
            $"Superuser '{entry.Superuser}' has no password, so hardening would lock you out of cluster '{entry.Id}'. " +
            $"Set one first with `empg role password {entry.Superuser}`, then run this again.");
    }
}
