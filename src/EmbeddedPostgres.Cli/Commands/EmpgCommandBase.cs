using System.Globalization;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core;

namespace EmbeddedPostgres.Cli.Commands;

/// <summary>
/// Base for every empg command, supplying the options that apply everywhere and turning
/// expected failures into a clean message plus a non-zero exit code.
/// </summary>
public abstract class EmpgCommandBase : ICommand
{
    /// <summary>
    /// Run as if empg was started in this directory, mirroring <c>git -C</c>.
    /// </summary>
    [CommandOption("instance", 'i', Description = "Registered instance to act on. Defaults to the active instance.")]
    public string? Instance { get; set; }

    [CommandOption("directory", 'C', Description = "Act on the instance in this directory, registered or not.")]
    public string? Directory { get; set; }

    /// <summary>
    /// How this invocation selected an instance, for handing to the resolver.
    /// </summary>
    protected EmpgSelector Selector => new(Instance, Directory);

    [CommandOption("json", Description = "Emit machine-readable JSON instead of text.")]
    public bool Json { get; set; }

    [CommandOption("quiet", 'q', Description = "Suppress informational output.")]
    public bool Quiet { get; set; }

    [CommandOption("connect-timeout", Description = "Seconds to wait when connecting to a cluster before giving up. 0 waits indefinitely.")]
    public int ConnectTimeoutSecs { get; set; } = DefaultConnectTimeoutSecs;

    /// <summary>
    /// Without a limit, connecting to an address nothing is listening on blocks on the operating
    /// system's TCP timeout, which can be minutes. Ten seconds is generous for the local and
    /// LAN clusters empg manages while still failing fast enough to be diagnosable.
    /// </summary>
    private const int DefaultConnectTimeoutSecs = 10;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var output = new OutputWriter(console, Json, Quiet);
        ApplyConnectTimeout();

        try
        {
            await ExecuteAsync(console, output).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is EmpgException or PgCoreException)
        {
            // Expected, actionable failures. CommandException is CliFx's channel for reporting
            // these as a plain message plus an exit code, rather than a stack trace.
            throw new CommandException(ex.Message, 1);
        }
    }

    /// <summary>
    /// Bounds how long libpq clients spend connecting.
    /// </summary>
    /// <remarks>
    /// psql, pg_dump and pg_restore are spawned as child processes and inherit this process's
    /// environment, so setting PGCONNECT_TIMEOUT here reaches all of them without every controller
    /// having to plumb it through.
    /// </remarks>
    private void ApplyConnectTimeout()
    {
        if (ConnectTimeoutSecs <= 0)
        {
            // Explicitly opting out: leave any inherited value alone.
            return;
        }

        Environment.SetEnvironmentVariable(
            "PGCONNECT_TIMEOUT",
            ConnectTimeoutSecs.ToString(CultureInfo.InvariantCulture));
    }

    protected abstract ValueTask ExecuteAsync(IConsole console, OutputWriter output);
}
