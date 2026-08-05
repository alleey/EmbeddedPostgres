namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// Runs SQL that contains a secret without exposing it.
/// </summary>
/// <remarks>
/// The library issues SQL as <c>psql -c "&lt;sql&gt;"</c>, which places the statement in the child
/// process's command line where any user on the machine can read it from the process list, and
/// where shell history may keep a copy. Statements carrying a password are therefore written to a
/// short-lived file and executed with <c>psql -f</c> instead.
/// </remarks>
public static class PgSecretSql
{
    public static async Task ExecuteAsync(
        PgDataCluster cluster,
        string sql,
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        // A per-user temp directory keeps the window where the file exists off shared paths.
        var path = Path.Combine(Path.GetTempPath(), $"empg-{Guid.NewGuid():N}.sql");

        try
        {
            await File.WriteAllTextAsync(path, sql, cancellationToken).ConfigureAwait(false);
            await cluster.ExecuteFileAsync(path, database, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Losing the race to delete a temp file must not mask the operation's own outcome.
            }
        }
    }

    /// <summary>
    /// Escapes a value for use inside a single-quoted SQL literal.
    /// </summary>
    public static string Literal(string value) => value.Replace("'", "''");
}
