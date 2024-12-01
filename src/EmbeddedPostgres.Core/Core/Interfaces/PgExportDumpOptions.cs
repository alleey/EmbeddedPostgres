using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

public record PgExportDumpOptions
{
    public string DatabaseName { get; set; }
    public string Target { get; set; }
    /// <summary>
    /// Backup file format, c|d|t|p (custom, directory, tar, plain text(default))
    /// </summary>
    public string TargetFormat { get; set; }

    public string RunAsUser { get; set; }
    public string RunAsPassword { get; set; }

    public int? MaxParallelJobs { get; set; }

    /// <summary>
    /// Compress as specified METHOD[:DETAIL]
    /// </summary>
    public int? CompressionMethod { get; set; }
    public bool? DumpOnlyData { get; set; }
    public bool? DumpOnlySchema { get; set; }

    public bool? UseInsertsStatements { get; set; }
    public bool? IncludeColumnNamesInInsertStatements { get; set; }
    public bool? IncludeDropStatements { get; set; }
    public bool? IncludeCreateStatements { get; set; }
    public bool? IncludeIfExistsStatements { get; set; }
    public string IncludeForeignData { get; set; }

    public string LockWaitTimeout { get; set; }
    public string SyncMethod { get; set; }
    public string Extension { get; set; }
    public string Encoding { get; set; }
    public string SuperUser { get; set; }
    public string Table { get; set; }

    public bool? DisableDollarQuoting { get; set; }
    public bool? DisableTriggers { get; set; }
    public bool? EnableRowSecurity { get; set; }
    public int? ExtraFloatDigits { get; set; }
    public string Filter { get; set; }

    public string ExcludeSchema { get; set; }
    public string ExcludeTable { get; set; }
    public string ExcludeExtension { get; set; }
    public string ExcludeTableAndChildren { get; set; }
    public string ExcludeTableData { get; set; }
    public string ExcludeTableDataAndChildren { get; set; }

    public bool? NoComments { get; set; }
    public bool? NoOwner { get; set; }
    public bool? NoPrivileges { get; set; }
    public bool? NoPublications { get; set; }
    public bool? NoSecurityLabels { get; set; }
    public bool? NoSubscriptions { get; set; }
    public bool? NoSync { get; set; }
    public bool? NoTableAccessMethod { get; set; }
    public bool? NoTablespaces { get; set; }
    public bool? NoToastCompression { get; set; }
    public bool? NoUnloggedTableData { get; set; }

    public bool? OnConflictDoNothing { get; set; }
    public bool? QuoteAllIdentifiers { get; set; }
    public int? RowsPerInsert { get; set; }
    public string Section { get; set; }

    public string Snapshot { get; set; }
    public bool? StrictNames { get; set; }
    public bool? UseSetSessionAuthorization { get; set; }

    public List<string> SchemasToDump { get; set; } = new();
    public List<string> SchemasToExclude { get; set; } = new();
    public List<string> TablesToDump { get; set; } = new();
    public List<string> TablesToExclude { get; set; } = new();

    internal IEnumerable<string> Build()
    {
        var args = new List<string>();

        // General options
        if (!string.IsNullOrEmpty(DatabaseName)) { args.Add("-d"); args.Add(DatabaseName); }
        if (!string.IsNullOrEmpty(TargetFormat)) { args.Add("-F"); args.Add(TargetFormat); }
        if (!string.IsNullOrEmpty(Target)) { args.Add("-f"); args.Add(Target); }
        if (MaxParallelJobs.HasValue) { args.Add("-j"); args.Add(MaxParallelJobs.ToString()); }
        if (CompressionMethod.HasValue) { args.Add("--compress"); args.Add(CompressionMethod.ToString()); }

        // Data selection options
        if (DumpOnlyData == true) args.Add("-a");
        if (DumpOnlySchema == true) args.Add("-s");
        if (UseInsertsStatements == true) args.Add("--inserts");
        if (IncludeColumnNamesInInsertStatements == true) args.Add("--column-inserts");
        if (IncludeDropStatements == true) args.Add("--clean");
        if (IncludeCreateStatements == true) args.Add("--create");
        if (IncludeIfExistsStatements == true) args.Add("--if-exists");

        if (!string.IsNullOrEmpty(IncludeForeignData)) { args.Add("--include-foreign-data"); args.Add(IncludeForeignData); }

        // Security options
        if (!string.IsNullOrEmpty(SuperUser)) { args.Add("--superuser"); args.Add(SuperUser); }
        if (DisableDollarQuoting == true) args.Add("--no-dollar-quoting");
        if (DisableTriggers == true) args.Add("--disable-triggers");
        if (EnableRowSecurity == true) args.Add("--enable-row-security");
        if (ExtraFloatDigits.HasValue) { args.Add("--extra-float-digits"); args.Add(ExtraFloatDigits.ToString()); }

        // Filtering options
        if (!string.IsNullOrEmpty(Filter)) { args.Add("--filter"); args.Add(Filter); }

        // Exclusion options
        if (!string.IsNullOrEmpty(ExcludeSchema)) { args.Add("--exclude-schema"); args.Add(ExcludeSchema); }
        if (!string.IsNullOrEmpty(ExcludeTable)) { args.Add("--exclude-table"); args.Add(ExcludeTable); }

        // Boolean options
        if (NoComments == true) args.Add("--no-comments");
        if (NoOwner == true) args.Add("--no-owner");
        if (NoPrivileges == true) args.Add("--no-privileges");
        if (NoPublications == true) args.Add("--no-publications");
        if (NoSecurityLabels == true) args.Add("--no-security-labels");

        // Section and additional options
        if (!string.IsNullOrEmpty(Section)) { args.Add("--section"); args.Add(Section); }
        if (StrictNames == true) args.Add("--strict-names");
        if (UseSetSessionAuthorization == true) args.Add("--use-set-session-authorization");

        // Options that can be specified multiple times
        foreach (var schema in SchemasToDump) { args.Add("-n"); args.Add(schema); }
        foreach (var schema in SchemasToExclude) { args.Add("-N"); args.Add(schema); }
        foreach (var table in TablesToDump) { args.Add("-t"); args.Add(table); }
        foreach (var table in TablesToExclude) { args.Add("-T"); args.Add(table); }

        return args;
    }

    internal void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(DatabaseName))
        {
            throw new PgValidationException("Database name is required.");
        }

        if (DumpOnlyData == true && DumpOnlySchema == true)
        {
            throw new PgValidationException("Cannot use both --data-only (-a) and --schema-only (-s) options together.");
        }
    }
}
