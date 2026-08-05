using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Prints a libpq connection URI for a cluster.
/// </summary>
/// <remarks>
/// The URI carries the advertised host rather than the bind address, since it is meant to be handed
/// to a client. It never carries a password: a URI ends up in scripts, logs and environment
/// variables, so the secret is left for the caller to supply out of band.
/// </remarks>
[Command("uri", Description = "Print a connection URI for a cluster.")]
public partial class UriCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public UriCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to describe. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    [CommandOption("user", 'u', Description = "Role to connect as. Defaults to the cluster superuser.")]
    public string? User { get; set; }

    [CommandOption("database", 'd', Description = "Database to connect to.")]
    public string Database { get; set; } = "postgres";

    [CommandOption("host", Description = "Override the advertised host, for example a name remote clients resolve.")]
    public string? Host { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);

        var host = Host ?? entry.Host;
        var user = User ?? entry.Superuser;
        var uri = $"postgresql://{Uri.EscapeDataString(user)}@{host}:{entry.Port}/{Uri.EscapeDataString(Database)}";

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            uri,
            host,
            port = entry.Port,
            user,
            database = Database,
        }).ConfigureAwait(false);

        await output.LineAsync(uri).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(entry.ListenAddresses) && !IsLoopback(host))
        {
            await output.WarnAsync(
                $"Cluster '{entry.Id}' binds to loopback only, so this URI will not work from another machine. " +
                "Set the bind address with `empg listen` and permit the source with `empg hba allow`.").ConfigureAwait(false);
        }
    }

    private static bool IsLoopback(string host)
        => host is "localhost" or "127.0.0.1" or "::1";
}
