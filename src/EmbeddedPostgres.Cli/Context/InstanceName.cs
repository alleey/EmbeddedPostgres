using System.Text.RegularExpressions;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// Validation for instance names.
/// </summary>
/// <remarks>
/// Names are typed constantly and appear in messages next to paths, so they are kept to a shape
/// that cannot be confused for one: no separators, no whitespace, no leading dash.
/// </remarks>
public static partial class InstanceName
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex Pattern { get; }

    public static bool IsValid(string? name)
        => !string.IsNullOrWhiteSpace(name) && name.Length <= 64 && Pattern.IsMatch(name);

    /// <exception cref="EmpgException">Thrown when the name is not usable.</exception>
    public static string Require(string? name)
    {
        if (!IsValid(name))
        {
            throw new EmpgException(
                $"Instance name '{name}' is not valid. Use letters, digits, dot, dash or underscore, " +
                "starting with a letter or digit, up to 64 characters.");
        }

        return name!;
    }
}
