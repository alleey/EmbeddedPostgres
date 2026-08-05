using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbeddedPostgres.Core.Configuration;

/// <summary>
/// Reads and rewrites <c>pg_hba.conf</c>.
/// </summary>
public static class PgHbaFile
{
    public const string FileName = "pg_hba.conf";

    /// <summary>
    /// All records in the file, in evaluation order, paired with whether each sits inside the
    /// managed block.
    /// </summary>
    public static IReadOnlyList<(PgHbaRule Rule, bool Managed)> Read(string text)
    {
        var managed = PgManagedBlock.Read(text)
            .Select(line => PgHbaRule.TryParse(line, out var rule) ? rule.ToLine() : null)
            .Where(line => line is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<(PgHbaRule, bool)>();
        foreach (var line in text.Split('\n'))
        {
            if (PgHbaRule.TryParse(line, out var rule))
            {
                results.Add((rule, managed.Contains(rule.ToLine())));
            }
        }

        return results;
    }

    /// <summary>
    /// Replaces initdb's <c>trust</c> records so that no connection is admitted without proof of
    /// identity.
    /// </summary>
    /// <remarks>
    /// This is the one place that rewrites records outside the managed block, because the records
    /// being removed are exactly the ones initdb wrote. It is idempotent: a file that has already
    /// been hardened contains no <c>trust</c> left to match.
    /// <para>
    /// Replication records matter as much as ordinary ones — <c>trust</c> there hands out a full
    /// copy of the data via a base backup.
    /// </para>
    /// </remarks>
    /// <param name="text">Current file contents.</param>
    /// <param name="method">Authentication method to substitute for <c>trust</c>.</param>
    public static string HardenDefaults(string text, string method = "scram-sha-256")
    {
        var lines = text.Split('\n');
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var trimmedEnd = line.TrimEnd('\r');

            if (!PgHbaRule.TryParse(trimmedEnd, out var rule)
                || !rule.Method.Equals("trust", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(line);
                continue;
            }

            // Preserve the record's leading whitespace so the file keeps its original shape.
            var indent = trimmedEnd[..(trimmedEnd.Length - trimmedEnd.TrimStart().Length)];
            result.Add(indent + (rule with { Method = method }).ToLine());
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// True when any record still uses <c>trust</c>.
    /// </summary>
    public static bool HasTrustRules(string text)
        => Read(text).Any(entry => entry.Rule.Method.Equals("trust", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the file with <paramref name="rule"/> present in the managed block.
    /// </summary>
    /// <remarks>
    /// A record covering the same connections is replaced rather than duplicated, so re-running
    /// with a different method updates in place.
    /// </remarks>
    public static string Allow(string text, PgHbaRule rule)
    {
        var rules = ManagedRules(text).Where(existing => !existing.Covers(rule)).ToList();
        rules.Add(rule);

        return PgManagedBlock.Write(text, Order(rules));
    }

    /// <summary>
    /// Orders records narrowest first, so that adding a broad rule cannot shadow a specific one
    /// that happens to have been added later.
    /// </summary>
    /// <remarks>
    /// OrderByDescending is stable, so records of equal specificity keep the order they were
    /// added in.
    /// </remarks>
    private static IReadOnlyList<string> Order(IEnumerable<PgHbaRule> rules)
        => rules
            .OrderByDescending(rule => rule.Specificity)
            .Select(rule => rule.ToLine())
            .ToList();

    /// <summary>
    /// Returns the file with every managed record covering the same connections as
    /// <paramref name="rule"/> removed, along with whether anything matched.
    /// </summary>
    public static (string Text, bool Removed) Revoke(string text, PgHbaRule rule)
    {
        var rules = ManagedRules(text);
        var remaining = rules.Where(existing => !existing.Covers(rule)).ToList();

        if (remaining.Count == rules.Count)
        {
            return (text, false);
        }

        var updated = remaining.Count == 0
            ? PgManagedBlock.Remove(text)
            : PgManagedBlock.Write(text, Order(remaining));

        return (updated, true);
    }

    private static List<PgHbaRule> ManagedRules(string text)
    {
        var rules = new List<PgHbaRule>();
        foreach (var line in PgManagedBlock.Read(text))
        {
            if (PgHbaRule.TryParse(line, out var rule))
            {
                rules.Add(rule);
            }
        }
        return rules;
    }
}
