using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbeddedPostgres.Core.Configuration;

/// <summary>
/// A single <c>pg_hba.conf</c> record.
/// </summary>
/// <remarks>
/// pg_hba is evaluated top to bottom and the first matching record decides the outcome, so the
/// order rules are written in is part of their meaning.
/// </remarks>
public sealed record PgHbaRule
{
    /// <summary>Connection type: local, host, hostssl, hostnossl and so on.</summary>
    public string Type { get; init; } = "host";

    public string Database { get; init; } = "all";

    public string User { get; init; } = "all";

    /// <summary>CIDR or keyword such as <c>samenet</c>. Empty for <c>local</c> records.</summary>
    public string Address { get; init; } = string.Empty;

    public string Method { get; init; } = "scram-sha-256";

    /// <summary>Trailing options such as <c>map=empg</c>.</summary>
    public string Options { get; init; } = string.Empty;

    /// <summary>
    /// True when the record can only ever match a connection originating on this machine.
    /// </summary>
    /// <remarks>
    /// Used to keep <c>trust</c> — which accepts anyone claiming a role name, with no credential —
    /// from being granted to addresses reachable from the network.
    /// </remarks>
    public bool IsLoopback
    {
        get
        {
            if (Type.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Address is "127.0.0.1/32" or "::1/128" or "localhost" or "127.0.0.1" or "::1";
        }
    }

    /// <summary>
    /// How narrowly this record matches. Higher is narrower.
    /// </summary>
    /// <remarks>
    /// pg_hba stops at the first matching record, so a broad record placed above a narrow one
    /// silently decides connections the narrow one was written for — a rule for <c>samenet</c>
    /// will answer for an address range you later admitted specifically. Ordering the managed
    /// block by this score keeps the narrower record in front regardless of the order rules were
    /// added in.
    /// </remarks>
    public int Specificity
    {
        get
        {
            var score = 0;

            if (!User.Equals("all", StringComparison.OrdinalIgnoreCase)) score += 100;
            if (!Database.Equals("all", StringComparison.OrdinalIgnoreCase)) score += 50;

            score += Address switch
            {
                "" => 0,
                "all" => 0,
                "samenet" => 5,
                "samehost" => 10,
                _ => AddressScore(Address),
            };

            return score;
        }
    }

    /// <summary>
    /// Scores a literal address: a single host outranks a range, and a tighter prefix outranks a
    /// looser one.
    /// </summary>
    private static int AddressScore(string address)
    {
        var slash = address.IndexOf('/');
        if (slash < 0)
        {
            // A bare address names exactly one host.
            return 40;
        }

        return int.TryParse(address[(slash + 1)..], out var prefix)
            ? 10 + prefix / 8
            : 10;
    }

    public static bool TryParse(string line, out PgHbaRule rule)
    {
        rule = new PgHbaRule();

        var text = line.Trim();
        if (text.Length == 0 || text.StartsWith('#'))
        {
            return false;
        }

        var tokens = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4)
        {
            return false;
        }

        // `local` records carry no address, so the method sits one column earlier.
        if (tokens[0].Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            rule = new PgHbaRule
            {
                Type = tokens[0],
                Database = tokens[1],
                User = tokens[2],
                Address = string.Empty,
                Method = tokens[3],
                Options = string.Join(' ', tokens.Skip(4)),
            };
            return true;
        }

        if (tokens.Length < 5)
        {
            return false;
        }

        rule = new PgHbaRule
        {
            Type = tokens[0],
            Database = tokens[1],
            User = tokens[2],
            Address = tokens[3],
            Method = tokens[4],
            Options = string.Join(' ', tokens.Skip(5)),
        };
        return true;
    }

    /// <summary>
    /// True when this record governs the same connections as <paramref name="other"/>, ignoring
    /// the authentication method. Used to find the record a revoke should remove.
    /// </summary>
    public bool Covers(PgHbaRule other)
        => Type.Equals(other.Type, StringComparison.OrdinalIgnoreCase)
        && Database.Equals(other.Database, StringComparison.OrdinalIgnoreCase)
        && User.Equals(other.User, StringComparison.OrdinalIgnoreCase)
        && Address.Equals(other.Address, StringComparison.OrdinalIgnoreCase);

    public string ToLine()
    {
        var columns = Type.Equals("local", StringComparison.OrdinalIgnoreCase)
            ? new[] { Type, Database, User, Method }
            : new[] { Type, Database, User, Address, Method };

        var line = string.Join(' ', columns);
        return string.IsNullOrWhiteSpace(Options) ? line : $"{line} {Options}";
    }

    public override string ToString() => ToLine();
}
