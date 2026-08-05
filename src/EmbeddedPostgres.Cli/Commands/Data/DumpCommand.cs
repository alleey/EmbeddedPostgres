using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Exports a database with pg_dump.
/// </summary>
[Command("dump", Description = "Export a database to a dump file.")]
public partial class DumpCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public DumpCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "target", Description = "File to write the dump to.")]
    public required string Target { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to dump from. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    [CommandOption("database", 'd', Description = "Database to dump.")]
    public string? Database { get; set; }

    [CommandOption("format", 'F', Description = "pg_dump output format: p (plain), c (custom), d (directory) or t (tar).")]
    public string? Format { get; set; }

    [CommandOption("schema-only", Description = "Dump the schema without any data.")]
    public bool SchemaOnly { get; set; }

    [CommandOption("data-only", Description = "Dump the data without the schema.")]
    public bool DataOnly { get; set; }

    [CommandOption("jobs", 'j', Description = "Number of parallel jobs.")]
    public int? Jobs { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        if (SchemaOnly && DataOnly)
        {
            throw new EmpgException("--schema-only and --data-only are mutually exclusive.");
        }

        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var options = new PgExportDumpOptions
        {
            Target = Target,
            DatabaseName = Database,
            TargetFormat = Format,
            MaxParallelJobs = Jobs,
            DumpOnlySchema = SchemaOnly ? true : null,
            DumpOnlyData = DataOnly ? true : null,
        };

        await output.InfoAsync($"Dumping {entry.Id} to {Target} ...").ConfigureAwait(false);
        await cluster.ExportDumpAsync(options, cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, database = Database, target = Target }).ConfigureAwait(false);
        await output.SuccessAsync($"Wrote dump to {Target}").ConfigureAwait(false);
    }
}
