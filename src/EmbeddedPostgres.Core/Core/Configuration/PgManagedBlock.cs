using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbeddedPostgres.Core.Configuration;

/// <summary>
/// Edits a marked-off region of a PostgreSQL configuration file such as <c>pg_hba.conf</c> or
/// <c>pg_ident.conf</c>.
/// </summary>
/// <remarks>
/// Only the region between the markers is owned. Anything an operator writes outside it survives
/// untouched, and re-applying the same rules is a no-op, so the whole thing can be run on every
/// start without accumulating duplicates.
/// </remarks>
public static class PgManagedBlock
{
    public const string Begin = "# BEGIN empg (managed block - do not edit)";
    public const string End = "# END empg";

    /// <summary>
    /// Returns the lines currently inside the managed block, or empty when there is no block.
    /// </summary>
    public static IReadOnlyList<string> Read(string text)
    {
        var begin = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (begin < 0 || end <= begin)
        {
            return Array.Empty<string>();
        }

        return text[(begin + Begin.Length)..end]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .ToList();
    }

    /// <summary>
    /// Returns <paramref name="text"/> with the managed block's contents set to
    /// <paramref name="lines"/>, replacing an existing block in place or appending one.
    /// </summary>
    public static string Write(string text, IReadOnlyList<string> lines)
    {
        var block = string.Join('\n', new[] { Begin }.Concat(lines).Append(End));

        var begin = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (begin >= 0 && end > begin)
        {
            return text[..begin] + block + text[(end + End.Length)..];
        }

        var separator = text.Length == 0 || text.EndsWith('\n') ? string.Empty : "\n";
        return text + separator + block + "\n";
    }

    /// <summary>
    /// Removes the managed block entirely, leaving everything outside it untouched.
    /// </summary>
    public static string Remove(string text)
    {
        var begin = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (begin < 0 || end <= begin)
        {
            return text;
        }

        return text[..begin] + text[(end + End.Length)..].TrimStart('\n');
    }
}
