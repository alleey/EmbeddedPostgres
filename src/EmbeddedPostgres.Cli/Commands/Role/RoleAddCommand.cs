using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Role;

/// <summary>
/// Creates a role, optionally with a password read from standard input.
/// </summary>
[Command("role add", Description = "Create a database role.")]
public partial class RoleAddCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public RoleAddCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "name", Description = "Role name.")]
    public required string Name { get; set; }

    [CommandOption("password-stdin", Description = "Read the role's password from standard input.")]
    public bool PasswordStdin { get; set; }

    [CommandOption("superuser", Description = "Grant superuser. Prefer a normal role for anything clients connect as.")]
    public bool Superuser { get; set; }

    [CommandOption("createdb", Description = "Allow the role to create databases.")]
    public bool CreateDb { get; set; }

    [CommandOption("login", Description = "Allow the role to log in.")]
    public bool Login { get; set; } = true;

    [CommandOption("cluster", 'c', Description = "Cluster to act on. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var name = PgIdentifier.Require(Name, "Role name");

        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var password = PasswordStdin
            ? await RolePassword.ReadFromStdinAsync(console).ConfigureAwait(false)
            : null;

        var attributes = new List<string>
        {
            Login ? "LOGIN" : "NOLOGIN",
            Superuser ? "SUPERUSER" : "NOSUPERUSER",
            CreateDb ? "CREATEDB" : "NOCREATEDB",
        };

        if (password is not null)
        {
            attributes.Add($"PASSWORD '{PgSecretSql.Literal(password)}'");
        }

        // DO block so re-running is not an error when the role already exists.
        var sql = $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{PgSecretSql.Literal(name)}') THEN
                    CREATE ROLE {name};
                END IF;
            END
            $$;
            ALTER ROLE {name} WITH {string.Join(' ', attributes)};
            """;

        await PgSecretSql.ExecuteAsync(cluster, sql, cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            role = name,
            superuser = Superuser,
            login = Login,
            createdb = CreateDb,
            hasPassword = password is not null,
        }).ConfigureAwait(false);

        await output.SuccessAsync($"{entry.Id}: role '{name}' ready.").ConfigureAwait(false);

        if (password is null && Login)
        {
            await output.InfoAsync(
                $"No password set. Run `empg role password {name} --password-stdin` before hardening authentication.").ConfigureAwait(false);
        }
    }
}
