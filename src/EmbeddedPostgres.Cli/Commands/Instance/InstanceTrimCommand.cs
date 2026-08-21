using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Cli.Trim;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Deletes the files named by this major version's trim list.
/// </summary>
/// <remarks>
/// There is no undo, so the command reports before it acts: without <c>--yes</c> it only lists
/// what it would remove. The list itself is a file per major version, editable by whoever runs it.
/// </remarks>
[Command("instance trim", Description = "Delete the files listed for this instance's PostgreSQL version. Lists them unless --yes is given.")]
public partial class InstanceTrimCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public InstanceTrimCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandOption("list", Description = "Use this trim list instead of the built-in one for the instance's version.")]
    public string? ListPath { get; set; }

    [CommandOption("yes", Description = "Actually delete. Without this the command only reports.")]
    public bool Yes { get; set; }

    [CommandOption("force", 'f', Description = "Permit trimming an adopted instance, whose binaries empg did not install.")]
    public bool Force { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);

        // An adopted installation belongs to whoever put it there - an installer, a shared volume.
        // This is the same reasoning that makes `destroy --purge` refuse; trim can be undone by
        // reinstalling, so it offers an explicit override rather than refusing outright.
        if (context.Manifest.Adopted && !Force)
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} is an adopted instance: empg did not install these binaries, so trimming is refused. "
                + "Re-run with --force if you own this installation and intend to shrink it in place.");
        }

        var major = (context.Manifest.PostgresVersion ?? string.Empty).Split('.')[0].Trim();
        if (major.Length == 0)
        {
            throw new EmpgException(
                "This instance does not record a PostgreSQL version, so no trim list can be chosen. Pass --list explicitly.");
        }

        var trimList = TrimList.Load(major, ListPath);
        var matches = trimList.Match(context.InstanceDirectory);
        var sizes = matches.ToDictionary(m => m, m => Length(context.InstanceDirectory, m), StringComparer.OrdinalIgnoreCase);
        var total = sizes.Values.Sum();

        await output.JsonAsync(new
        {
            instance = context.InstanceDirectory,
            list = trimList.Origin,
            applied = Yes,
            files = matches.Count,
            bytes = total,
            delete = matches,
        }).ConfigureAwait(false);

        await output.InfoAsync(
            $"{(Yes ? "Trimming" : "Would trim")} {context.InstanceDirectory} using {trimList.Origin}").ConfigureAwait(false);

        if (matches.Count == 0)
        {
            await output.SuccessAsync("Nothing to delete.").ConfigureAwait(false);
            return;
        }

        // The full list runs to thousands of files; the large ones are what a reader checks.
        const int Shown = 20;
        await output.TableAsync(
            ["MB", "FILE"],
            matches.OrderByDescending(m => sizes[m]).Take(Shown)
                .Select(m => (IReadOnlyList<string>)new[] { (sizes[m] / 1048576.0).ToString("F1"), m })
                .ToList()).ConfigureAwait(false);

        if (matches.Count > Shown)
        {
            await output.LineAsync($"  ... and {matches.Count - Shown} more. Use --json for the full list.").ConfigureAwait(false);
        }

        if (!Yes)
        {
            await output.WarnAsync(
                $"Nothing was deleted. Re-run with --yes to remove {matches.Count} file(s) and free {Mb(total)} MB.").ConfigureAwait(false);
            return;
        }

        foreach (var relative in matches)
        {
            var full = Path.Combine(context.InstanceDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;
            File.SetAttributes(full, FileAttributes.Normal);
            File.Delete(full);
        }

        // Deleting files leaves the directories that held them; longest-first so nested ones go too.
        // System.IO.Directory is spelled out: EmpgCommandBase has a `Directory` property for the
        // -C option, which otherwise shadows the type here.
        foreach (var dir in System.IO.Directory
                     .EnumerateDirectories(context.InstanceDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (!System.IO.Directory.EnumerateFileSystemEntries(dir).Any())
            {
                System.IO.Directory.Delete(dir);
            }
        }

        await output.SuccessAsync($"Deleted {matches.Count} file(s), freeing {Mb(total)} MB.").ConfigureAwait(false);
    }

    private static long Length(string root, string relative)
    {
        try { return new FileInfo(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))).Length; }
        catch (IOException) { return 0; }
    }

    private static string Mb(long bytes) => (bytes / 1048576.0).ToString("F0");
}
