using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

public record PgRestoreDumpOptions
{
    // General options
    public string ConnectDatabaseName { get; set; }

    public string Source { get; set; }
    /// <summary>
    /// Backup file format, c|d|t (custom, directory, tar)
    /// </summary>
    public string SourceFormat { get; set; }

    public string RunAsUser { get; set; }
    public string RunAsPassword { get; set; }

    // Options controlling the restore
    public bool? RestoreOnlyData { get; set; }
    public bool? RestoreOnlySchema { get; set; }
    public bool? DropTargetDatabase { get; set; }
    public bool? CreateTargetDatabase { get; set; }

    public bool? ExitOnError { get; set; }
    public bool? SingleTransaction { get; set; }
    public bool? DisableTriggers { get; set; }
    public bool? EnableRowSecurity { get; set; }
    public string Filter { get; set; }
    public bool? UseIfExists { get; set; }
    public int? MaxParallelJobs { get; set; }

    public bool? NoComments { get; set; }
    public bool? NoDataForFailedTables { get; set; }
    public bool? NoPrivileges { get; set; }
    public bool? NoPublications { get; set; }
    public bool? NoRestoreOwnership { get; set; }
    public bool? NoSecurityLabels { get; set; }
    public bool? NoSubscriptions { get; set; }
    public bool? NoTableAccessMethod { get; set; }
    public bool? NoTablespaces { get; set; }

    public bool? StrictNames { get; set; }
    public int? TransactionSize { get; set; }
    public bool? UseSetSessionAuthorization { get; set; }

    public List<string> SchemasToRestore { get; set; } = new();
    public List<string> SchemasToExclude { get; set; } = new();
    public List<string> TablesToRestore { get; set; } = new();

    internal IEnumerable<string> Build()
    {
        var args = new List<string>();

        // General connection options
        if (!string.IsNullOrEmpty(ConnectDatabaseName))
        {
            args.Add("-d");
            args.Add(ConnectDatabaseName);
        }
        if (!string.IsNullOrEmpty(SourceFormat))
        {
            args.Add("-F");
            args.Add(SourceFormat);
        }

        // Restore behavior control options
        if (RestoreOnlyData == true) args.Add("-a");
        if (RestoreOnlySchema == true) args.Add("-s");
        if (DropTargetDatabase == true) args.Add("-c");
        if (CreateTargetDatabase == true) args.Add("-C");
        if (ExitOnError == true) args.Add("-e");
        if (SingleTransaction == true) args.Add("-1");
        if (DisableTriggers == true) args.Add("--disable-triggers");
        if (EnableRowSecurity == true) args.Add("--enable-row-security");

        // Ownership and privilege options
        if (NoRestoreOwnership == true) args.Add("-O");
        if (NoPrivileges == true) args.Add("-x");
        if (UseIfExists == true) args.Add("--if-exists");

        // Parallelization options
        if (MaxParallelJobs.HasValue)
        {
            args.Add("-j");
            args.Add(MaxParallelJobs.ToString());
        }

        // Error handling and data integrity options
        if (NoDataForFailedTables == true) args.Add("--no-data-for-failed-tables");
        if (StrictNames == true) args.Add("--strict-names");
        if (TransactionSize.HasValue)
        {
            args.Add("--transaction-size");
            args.Add(TransactionSize.ToString());
        }

        // Optional settings for specific exclusions or inclusion
        if (!string.IsNullOrEmpty(Filter))
        {
            args.Add("--filter");
            args.Add(Filter);
        }
        if (NoComments == true) args.Add("--no-comments");
        if (NoPublications == true) args.Add("--no-publications");
        if (NoSecurityLabels == true) args.Add("--no-security-labels");
        if (NoSubscriptions == true) args.Add("--no-subscriptions");
        if (NoTableAccessMethod == true) args.Add("--no-table-access-method");
        if (NoTablespaces == true) args.Add("--no-tablespaces");

        // Authorization and session settings
        if (UseSetSessionAuthorization == true) args.Add("--use-set-session-authorization");

        // Specify schemas, tables, and exclusions
        foreach (var schema in SchemasToRestore)
        {
            args.Add("-n");
            args.Add(schema);
        }
        foreach (var schema in SchemasToExclude)
        {
            args.Add("-N");
            args.Add(schema);
        }
        foreach (var table in TablesToRestore)
        {
            args.Add("-t");
            args.Add(table);
        }

        // Final source file or directory
        args.Add(Source);

        return args;
    }

    internal void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(Source))
        {
            throw new PgValidationException("Dump file name is required.");
        }

        if (string.IsNullOrEmpty(ConnectDatabaseName))
        {
            throw new PgValidationException("TargetDirectory database name (-d) is required.");
        }

        if (RestoreOnlyData == true && RestoreOnlySchema == true)
        {
            throw new PgValidationException("Cannot use both --data-only (-a) and --schema-only (-s) options together.");
        }
    }
}
