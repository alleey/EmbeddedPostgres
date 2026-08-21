using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmbeddedPostgres.Cli.Trim;

/// <summary>
/// The list of files <c>instance trim</c> deletes, one per PostgreSQL major version.
/// </summary>
/// <remarks>
/// Data rather than code: what a build ships changes every release, so the list is a file a user
/// can edit, not something compiled in. It is a DELETE list - nothing is removed unless a pattern
/// names it - so a wrong entry costs one file, never a working installation.
/// </remarks>
public sealed class TrimList
{
    private const string ResourcePrefix = "EmbeddedPostgres.Cli.TrimProfiles.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string Major { get; set; } = string.Empty;

    public List<string> Delete { get; set; } = new();

    /// <summary>Where this list came from, for reporting.</summary>
    public string Origin { get; private set; } = "embedded";

    /// <summary>
    /// Loads the list for a major version: an explicit file if given, otherwise the built-in one.
    /// </summary>
    public static TrimList Load(string major, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Trim list '{path}' does not exist.", path);
            }
            return Parse(File.ReadAllText(path), path);
        }

        using var stream = typeof(TrimList).Assembly.GetManifestResourceStream($"{ResourcePrefix}pg{major}.json");
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"No built-in trim list for PostgreSQL {major}. Shipped: {string.Join(", ", ShippedMajors())}. Pass --list to supply your own.");
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), $"built-in pg{major}.json");
    }

    public static IReadOnlyList<string> ShippedMajors()
        => typeof(TrimList).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[(ResourcePrefix.Length + 2)..].Replace(".json", string.Empty))
            .OrderByDescending(v => int.TryParse(v, out var i) ? i : 0)
            .ToList();

    /// <summary>Files under <paramref name="instanceDirectory"/> that the list names.</summary>
    public IReadOnlyList<string> Match(string instanceDirectory)
    {
        var patterns = Delete.Select(ToRegex).ToList();
        if (patterns.Count == 0) return Array.Empty<string>();

        return Directory.EnumerateFiles(instanceDirectory, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(instanceDirectory, f).Replace(Path.DirectorySeparatorChar, '/'))
            // empg's own state describes the instance; it is never installation payload.
            .Where(f => !f.StartsWith(".empg/", StringComparison.OrdinalIgnoreCase))
            .Where(f => patterns.Any(p => p.IsMatch(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TrimList Parse(string json, string origin)
    {
        var list = JsonSerializer.Deserialize<TrimList>(json, SerializerOptions)
            ?? throw new InvalidDataException($"Trim list '{origin}' is empty or malformed.");
        list.Origin = origin;
        return list;
    }

    /// <summary>
    /// One glob to one regex. <c>**</c> crosses directories, <c>*</c> and <c>?</c> do not.
    /// </summary>
    /// <remarks>
    /// Matching ignores case because both platforms EnterpriseDB publishes for - Windows and
    /// macOS - are case-insensitive by default, and a list written as <c>bin/*.EXE</c> should
    /// behave the same on either.
    /// </remarks>
    private static Regex ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        var g = glob.Replace('\\', '/');

        for (var i = 0; i < g.Length; i++)
        {
            if (g[i] == '*')
            {
                if (i + 1 < g.Length && g[i + 1] == '*')
                {
                    // 'a/**/b' should also match 'a/b', so the separator is optional.
                    if (i + 2 < g.Length && g[i + 2] == '/') { pattern.Append("(?:.*/)?"); i += 2; }
                    else { pattern.Append(".*"); i++; }
                }
                else
                {
                    pattern.Append("[^/]*");
                }
            }
            else if (g[i] == '?')
            {
                pattern.Append("[^/]");
            }
            else
            {
                pattern.Append(Regex.Escape(g[i].ToString()));
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
