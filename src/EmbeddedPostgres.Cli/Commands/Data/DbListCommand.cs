using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Lists the databases in a running cluster.
/// </summary>
[Command("db list", Description = "List databases in a cluster.")]
public partial class DbListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public DbListCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
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

        var databases = new List<PgDatabaseInfo>();
        await cluster.ListDatabasesAsync(
            (database, ct) =>
            {
                databases.Add(database);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            databases = databases.Select(d => new { d.Name, d.Owner, d.Encoding, d.Collate, d.Ctype }),
        }).ConfigureAwait(false);

        if (databases.Count == 0)
        {
            await output.LineAsync("No databases reported.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["NAME", "OWNER", "ENCODING", "COLLATE"],
            databases.Select(d => (IReadOnlyList<string>)new[]
            {
                d.Name ?? string.Empty,
                d.Owner ?? string.Empty,
                d.Encoding ?? string.Empty,
                d.Collate ?? string.Empty,
            }).ToList()).ConfigureAwait(false);
    }
}
