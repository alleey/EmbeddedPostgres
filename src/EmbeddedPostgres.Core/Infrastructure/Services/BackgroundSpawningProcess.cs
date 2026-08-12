using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres;

/// <summary>
/// Runs a command that leaves a long-lived process behind, such as <c>pg_ctl start</c>.
/// </summary>
/// <remarks>
/// A launcher like <c>pg_ctl start</c> exits quickly but leaves the PostgreSQL server running, and the
/// server keeps whatever standard output and error handles it was given open for as long as it runs.
/// Anything waiting for end-of-stream on those handles therefore waits for the lifetime of the cluster.
/// <para>
/// Two things are needed to contain that. The child's output goes to pipes owned here rather than to the
/// caller's, so that the handles the server holds die with this process instead of hanging whoever is
/// reading our output. And on Windows the inherit flag is cleared on our own standard handles for the
/// duration of the spawn, because <c>CreateProcess</c> with handle inheritance enabled passes on every
/// inheritable handle, not only the ones placed in the child's standard slots - so the server would
/// otherwise pick up ours regardless of the redirection.
/// </para>
/// <para>
/// The pipes are drained but never waited on: draining stops the server blocking on a full buffer, and
/// not waiting is what lets this return as soon as the launcher exits.
/// </para>
/// </remarks>
internal static class BackgroundSpawningProcess
{
    /// <summary>
    /// The inherit flags are process-wide, so only one spawn may sit inside that window at a time.
    /// </summary>
    private static readonly SemaphoreSlim SpawnLock = new(1, 1);

    public static async Task<ExecuteResult> ExecuteAsync(
        string binaryPath,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environmentVariables,
        string workingDirectory,
        bool throwOnNonZeroExitCode,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(binaryPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments ?? [])
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables != null)
        {
            foreach (var variable in environmentVariables)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        logger.LogInformation($"Execute (leaves background process): {binaryPath} {string.Join(' ', arguments ?? [])}, in {workingDirectory}");

        Process process;
        try
        {
            process = await StartWithoutLeakingOurHandlesAsync(startInfo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PgCommandExecutionException(-1, $"{binaryPath} failed to start: {ex.Message}");
        }

        using (process)
        {
            // Drain so a full pipe buffer can never stall the server, but never wait for
            // end-of-stream: that only arrives once the server itself exits.
            _ = DrainAsync(process.StandardOutput);
            _ = DrainAsync(process.StandardError);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var exitCode = process.ExitCode;
            logger.LogDebug($"{binaryPath} finished with exit code {exitCode}");

            if (throwOnNonZeroExitCode && exitCode != 0)
            {
                throw new PgCommandExecutionException(exitCode, $"{binaryPath} failed with error [{exitCode}]");
            }

            return new ExecuteResult(exitCode);
        }
    }

    /// <summary>
    /// Starts the process, withholding our own standard handles from inheritance where the platform
    /// would otherwise pass them on.
    /// </summary>
    /// <remarks>
    /// Only Windows needs the extra step. <c>CreateProcess</c> with handle inheritance enabled hands the
    /// child every inheritable handle rather than only the ones placed in its standard slots, so the
    /// redirection alone does not keep ours away from the server. On Unix the descriptors .NET holds are
    /// opened close-on-exec, so the redirected slots are all the child receives and plain
    /// <see cref="Process.Start(ProcessStartInfo)"/> is enough.
    /// </remarks>
    private static async Task<Process> StartWithoutLeakingOurHandlesAsync(ProcessStartInfo startInfo)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Process.Start(startInfo);
        }

        await SpawnLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return Windows.StartWithStandardHandlesWithheld(startInfo);
        }
        finally
        {
            SpawnLock.Release();
        }
    }

    /// <summary>
    /// The Windows-only interop. Kept apart so that the platform boundary is visible rather than
    /// implied, and so the compiler enforces the guard on every caller.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static class Windows
    {
        private const int StdOutputHandle = -11;
        private const int StdErrorHandle = -12;
        private const uint HandleFlagInherit = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetHandleInformation(IntPtr hObject, out uint lpdwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        public static Process StartWithStandardHandlesWithheld(ProcessStartInfo startInfo)
        {
            var restore = new List<(IntPtr Handle, uint Flags)>();

            foreach (var id in new[] { StdOutputHandle, StdErrorHandle })
            {
                var handle = GetStdHandle(id);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                {
                    continue;
                }

                if (GetHandleInformation(handle, out var flags) && (flags & HandleFlagInherit) != 0)
                {
                    SetHandleInformation(handle, HandleFlagInherit, 0);
                    restore.Add((handle, flags));
                }
            }

            try
            {
                return Process.Start(startInfo);
            }
            finally
            {
                foreach (var (handle, flags) in restore)
                {
                    SetHandleInformation(handle, HandleFlagInherit, flags & HandleFlagInherit);
                }
            }
        }
    }

    private static Task DrainAsync(StreamReader reader)
        => reader.BaseStream.CopyToAsync(Stream.Null).ContinueWith(
            // The server owns the write end, so the copy ends by being torn down rather than by
            // completing; failures here are expected and carry no information worth surfacing.
            static t => { _ = t.Exception; },
            TaskContinuationOptions.ExecuteSynchronously);
}
