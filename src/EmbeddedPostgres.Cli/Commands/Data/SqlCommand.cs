using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Runs SQL against a cluster, either inline or from a file.
/// </summary>
[Command("sql", Description = "Execute SQL against a cluster.")]
public partial class SqlCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public SqlCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "statement", Description = "SQL to execute. Omit when using --sql or --file.")]
    public string? Statement { get; set; }

    [CommandOption("sql", 's', Description = "SQL to execute, as an option instead of the positional statement.")]
    public string? Sql { get; set; }

    [CommandOption("file", 'f', Description = "Execute SQL from this file instead of an inline statement.")]
    public string? File { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to run against. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    [CommandOption("database", 'd', Description = "Database to connect to.")]
    public string? Database { get; set; }

    [CommandOption("user", 'u', Description = "User to connect as. Defaults to the cluster superuser.")]
    public string? User { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        var provided = 0;
        if (!string.IsNullOrWhiteSpace(Statement)) provided++;
        if (!string.IsNullOrWhiteSpace(Sql)) provided++;
        if (!string.IsNullOrWhiteSpace(File)) provided++;

        if (provided != 1)
        {
            throw new EmpgException(
                "Provide exactly one of: a positional SQL statement, --sql, or --file.");
        }

        var statement = string.IsNullOrWhiteSpace(Statement) ? Sql : Statement;

        if (File is not null && !System.IO.File.Exists(File))
        {
            throw new EmpgException($"SQL file not found: {File}");
        }

        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var lines = new List<string>();
        Task Collect(string line, CancellationToken ct)
        {
            lines.Add(line);
            return output.LineAsync(line);
        }

        if (File is not null)
        {
            await cluster.ExecuteFileAsync(File, Database, User, Collect, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await cluster.ExecuteSqlAsync(statement!, Database, User, Collect, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await output.JsonAsync(new { cluster = entry.Id, database = Database, output = lines }).ConfigureAwait(false);
    }
}
