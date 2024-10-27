using System;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the formatting options for SQL query results.
/// </summary>
public record PgSqlResultFormat
{
    /// <summary>
    /// Specifies the output format of the SQL results.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Aligns the output in a tabular format.
        /// </summary>
        Aligned,

        /// <summary>
        /// Outputs results without alignment.
        /// </summary>
        UnAligned,

        /// <summary>
        /// Outputs the results in CSV format.
        /// </summary>
        CSV
    }

    /// <summary>
    /// Gets or sets the desired output format for the SQL results.
    /// Defaults to <see cref="OutputFormat.CSV"/>.
    /// </summary>
    public OutputFormat Format { get; init; } = OutputFormat.CSV;

    /// <summary>
    /// Gets or sets a value indicating whether to include headers in the output.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>
    /// Gets or sets the character used to separate fields in the output.
    /// Defaults to "|".
    /// </summary>
    public string FieldSeparator { get; init; } = "|";

    /// <summary>
    /// Gets or sets the string used to separate records in the output.
    /// Defaults to <see cref="Environment.NewLine"/>.
    /// </summary>
    public string RecordSeparator { get; init; } = Environment.NewLine;

    /// <summary>
    /// Gets or sets the output file path for the results.
    /// If <c>null</c>, the output will not be written to a file.
    /// </summary>
    public string OutputFile { get; init; } = null;

    /// <summary>
    /// Gets the default configuration for SQL result formatting.
    /// </summary>
    public static PgSqlResultFormat Default { get; } = new();
}
