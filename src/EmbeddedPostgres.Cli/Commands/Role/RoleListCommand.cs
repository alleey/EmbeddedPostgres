using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Role;

/// <summary>
/// Lists roles and whether each has a password.
/// </summary>
[Command("role list", Description = "List database roles.")]
public partial class RoleListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public RoleListCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to query. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var lines = new List<string>();

        // Reports only whether a password exists, never the stored verifier itself.
        await cluster.ExecuteSqlAsync(
            """
            SELECT rolname, rolsuper, rolcanlogin, rolpassword IS NOT NULL AS has_password
            FROM pg_authid ORDER BY rolname;
            """,
            listener: (line, ct) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line.Trim());
                }
                return Task.CompletedTask;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // psql emits CSV: drop the header row.
        var rows = lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(columns => columns.Length >= 4)
            .ToList();

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            roles = rows.Select(c => new
            {
                name = c[0],
                superuser = c[1] == "t",
                canLogin = c[2] == "t",
                hasPassword = c[3] == "t",
            }),
        }).ConfigureAwait(false);

        await output.TableAsync(
            ["ROLE", "SUPERUSER", "LOGIN", "PASSWORD"],
            rows.Select(c => (IReadOnlyList<string>)new[]
            {
                c[0],
                c[1] == "t" ? "yes" : "no",
                c[2] == "t" ? "yes" : "no",
                c[3] == "t" ? "set" : "none",
            }).ToList()).ConfigureAwait(false);
    }
}
