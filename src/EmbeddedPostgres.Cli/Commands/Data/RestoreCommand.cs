using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Data;

/// <summary>
/// Imports a dump with pg_restore.
/// </summary>
[Command("restore", Description = "Restore a database from a dump file.")]
public partial class RestoreCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public RestoreCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "source", Description = "Dump file to restore from.")]
    public required string Source { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to restore into. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    [CommandOption("database", 'd', Description = "Database to connect to.")]
    public string? Database { get; set; }

    [CommandOption("format", 'F', Description = "pg_restore input format: c (custom), d (directory) or t (tar).")]
    public string? Format { get; set; }

    [CommandOption("clean", Description = "Drop existing objects before recreating them.")]
    public bool Clean { get; set; }

    [CommandOption("create", Description = "Create the target database before restoring into it.")]
    public bool Create { get; set; }

    [CommandOption("exit-on-error", Description = "Stop at the first error instead of continuing.")]
    public bool ExitOnError { get; set; }

    [CommandOption("jobs", 'j', Description = "Number of parallel jobs.")]
    public int? Jobs { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        if (!File.Exists(Source) && !System.IO.Directory.Exists(Source))
        {
            throw new EmpgException($"Dump not found: {Source}");
        }

        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var server = await serverFactory.OpenAsync(context, entry.Id, cancellationToken).ConfigureAwait(false);
        var cluster = server.GetClusterByUniqueId(entry.Id);

        var options = new PgRestoreDumpOptions
        {
            Source = Source,
            ConnectDatabaseName = Database,
            SourceFormat = Format,
            MaxParallelJobs = Jobs,
            DropTargetDatabase = Clean ? true : null,
            CreateTargetDatabase = Create ? true : null,
            ExitOnError = ExitOnError ? true : null,
        };

        await output.InfoAsync($"Restoring {Source} into {entry.Id} ...").ConfigureAwait(false);
        await cluster.ImportDumpAsync(options, cancellationToken).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, database = Database, source = Source }).ConfigureAwait(false);
        await output.SuccessAsync($"Restored {Source}").ConfigureAwait(false);
    }
}
