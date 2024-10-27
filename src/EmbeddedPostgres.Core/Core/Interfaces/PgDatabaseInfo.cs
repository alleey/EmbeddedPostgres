namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents information about a PostgreSQL database.
/// </summary>
public record PgDatabaseInfo
{
    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the owner of the database.
    /// </summary>
    public string Owner { get; init; }

    /// <summary>
    /// Gets the encoding used by the database.
    /// </summary>
    public string Encoding { get; init; }

    /// <summary>
    /// Gets the locale provider for the database.
    /// </summary>
    public string LocaleProvider { get; init; }

    /// <summary>
    /// Gets the collation setting for the database.
    /// </summary>
    public string Collate { get; init; }

    /// <summary>
    /// Gets the character classification setting for the database.
    /// </summary>
    public string Ctype { get; init; }

    /// <summary>
    /// Gets the locale setting for the database.
    /// </summary>
    public string Locale { get; init; }

    /// <summary>
    /// Gets the ICU rules associated with the database.
    /// </summary>
    public string ICURules { get; init; }

    /// <summary>
    /// Gets the access privileges for the database.
    /// </summary>
    public string AccessPrivileges { get; init; }
}
