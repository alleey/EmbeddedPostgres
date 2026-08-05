using System.Text.Json;
using System.Text.Json.Serialization;
using CliFx.Infrastructure;

namespace EmbeddedPostgres.Cli.Output;

/// <summary>
/// Renders command results as either human-readable text or JSON.
/// </summary>
/// <remarks>
/// Commands write through this rather than to the console directly, so that <c>--json</c> produces
/// a clean document on stdout with no interleaved progress chatter.
/// </remarks>
public class OutputWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Keeps nested records (which are PascalCase in C#) consistent with the hand-written
        // anonymous objects, so --json presents one casing convention throughout.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConsole console;
    private readonly bool json;
    private readonly bool quiet;

    public OutputWriter(IConsole console, bool json, bool quiet)
    {
        this.console = console;
        this.json = json;
        this.quiet = quiet;
    }

    public bool IsJson => json;

    /// <summary>
    /// Writes a status line. Suppressed under <c>--json</c> and <c>--quiet</c> so it never
    /// contaminates machine-readable output.
    /// </summary>
    public async Task InfoAsync(string message)
    {
        if (json || quiet)
        {
            return;
        }
        await console.Output.WriteLineAsync(message).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a line the user asked for. Suppressed under <c>--json</c> only.
    /// </summary>
    public async Task LineAsync(string message)
    {
        if (json)
        {
            return;
        }
        await console.Output.WriteLineAsync(message).ConfigureAwait(false);
    }

    public async Task WarnAsync(string message)
    {
        if (json)
        {
            return;
        }
        using (console.WithForegroundColor(ConsoleColor.Yellow))
        {
            await console.Error.WriteLineAsync(message).ConfigureAwait(false);
        }
    }

    public async Task SuccessAsync(string message)
    {
        if (json || quiet)
        {
            return;
        }
        using (console.WithForegroundColor(ConsoleColor.Green))
        {
            await console.Output.WriteLineAsync(message).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Emits the structured result of a command. Only produces output under <c>--json</c>.
    /// </summary>
    public async Task JsonAsync(object payload)
    {
        if (!json)
        {
            return;
        }
        await console.Output.WriteLineAsync(JsonSerializer.Serialize(payload, SerializerOptions)).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a left-aligned table. Skipped entirely under <c>--json</c>.
    /// </summary>
    public async Task TableAsync(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (json)
        {
            return;
        }

        var widths = headers.Select(h => h.Length).ToArray();
        foreach (var row in rows)
        {
            for (var i = 0; i < widths.Length && i < row.Count; i++)
            {
                widths[i] = Math.Max(widths[i], row[i]?.Length ?? 0);
            }
        }

        await console.Output.WriteLineAsync(Format(headers, widths)).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await console.Output.WriteLineAsync(Format(row, widths)).ConfigureAwait(false);
        }
    }

    private static string Format(IReadOnlyList<string> cells, int[] widths)
    {
        var parts = new List<string>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i] ?? string.Empty;
            // Don't pad the final column; trailing spaces are noise when piping output.
            parts.Add(i == cells.Count - 1 ? cell : cell.PadRight(widths[i]));
        }
        return string.Join("  ", parts);
    }
}
