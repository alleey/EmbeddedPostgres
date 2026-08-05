using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Role;

/// <summary>
/// Sets a role's password.
/// </summary>
[Command("role password", Description = "Set a role's password, read from standard input.")]
public partial class RolePasswordCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public RolePasswordCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "name", Description = "Role whose password to set.")]
    public required string Name { get; set; }

    [CommandOption("password-stdin", Description = "Read the password from standard input. Required.")]
    public bool PasswordStdin { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to act on. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var name = PgIdentifier.Require(Name, "Role name");

        if (!PasswordStdin)
        {
            throw new EmpgException(
                "Pass --password-stdin and pipe the password in. There is no option to give it as an argument, " +
                "because arguments are visible in the process list and shell history.");
        }

        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var password = await RolePassword.ReadFromStdinAsync(console).ConfigureAwait(false);

        await PgSecretSql.ExecuteAsync(
            cluster,
            $"ALTER ROLE {name} WITH PASSWORD '{PgSecretSql.Literal(password)}';",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, role = name, passwordSet = true }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: password set for '{name}'.").ConfigureAwait(false);
    }
}
