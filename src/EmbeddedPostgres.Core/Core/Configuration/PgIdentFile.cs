namespace EmbeddedPostgres.Core.Configuration;

/// <summary>
/// Constants for <c>pg_ident.conf</c>, which maps operating-system principals to database roles.
/// </summary>
/// <remarks>
/// The file shares <c>pg_hba.conf</c>'s comment syntax, so <see cref="PgManagedBlock"/> handles the
/// managed region for both. Unlike pg_hba, entries here accumulate rather than compete: a principal
/// mapping does not shadow the ones after it.
/// </remarks>
public static class PgIdentFile
{
    public const string FileName = "pg_ident.conf";
}
