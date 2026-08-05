using System.Text.RegularExpressions;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// Validation for names that reach both SQL and PostgreSQL configuration files.
/// </summary>
/// <remarks>
/// A role name written into <c>pg_hba.conf</c> cannot be quoted or escaped the way a SQL literal
/// can — the file is whitespace-delimited. Rather than escape differently per destination, names
/// are restricted to a shape that is safe everywhere.
/// </remarks>
public static partial class PgIdentifier
{
    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex Pattern { get; }

    public static bool IsValid(string? name)
        => !string.IsNullOrEmpty(name) && name.Length <= 63 && Pattern.IsMatch(name);

    /// <exception cref="EmpgException">Thrown when the name is not usable unquoted.</exception>
    public static string Require(string? name, string what)
    {
        if (!IsValid(name))
        {
            throw new EmpgException(
                $"{what} '{name}' must be a lowercase identifier: a letter or underscore followed by " +
                "letters, digits or underscores, at most 63 characters.");
        }

        return name!;
    }
}
