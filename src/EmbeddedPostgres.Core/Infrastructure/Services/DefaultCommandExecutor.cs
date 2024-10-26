using CliWrap;
using CliWrap.EventStream;
using CliWrap.Exceptions;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres;

internal class DefaultCommandExecutor : ICommandExecutor
{
    private readonly ILogger<DefaultCommandExecutor> logger;

    public DefaultCommandExecutor(ILogger<DefaultCommandExecutor> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Executes a command asynchronously and returns the result.
    /// </summary>
    /// <param name="binaryPath">The path to the executable binary to run.</param>
    /// <param name="arguments">An optional list of arguments to pass to the executable.</param>
    /// <param name="workingDirectory">The working directory from which to execute the command.</param>
    /// <param name="validateNonZeroExitCode">
    /// Indicates whether to validate the exit code; if true and the exit code is non-zero, an exception may be thrown.
    /// </param>
    /// <param name="outputListener">
    /// An optional listener for standard output messages. It is recommended to use this only if the process is guaranteed to exit.
    /// </param>
    /// <param name="errorListener">
    /// An optional listener for error messages. It is recommended to use this only if the process is guaranteed to exit.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to signal cancellation of the command execution.</param>
    /// <returns>A task representing the asynchronous operation, with a result of type <see cref="ExecuteResult"/>.</returns>
    /// <remarks>
    /// Do not use the <paramref name="outputListener"/> and <paramref name="errorListener"/> unless the process is guaranteed to 
    /// exit. For example, <c>pg_ctl start</c> may hang if its output is captured, possibly due to output handles kept open by child processes.
    /// </remarks>
    public async Task<ExecuteResult> ExecuteAsync(
        string binaryPath,
        IEnumerable<string> arguments = default,
        string workingDirectory = default,
        bool throwOnNonZeroExitCode = true,
        Func<string, CancellationToken, Task> outputListener = default,
        Func<string, CancellationToken, Task> errorListener = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation($"Execute: {binaryPath} {string.Join(' ', arguments)}, in {workingDirectory}");

            var command = Cli.Wrap(binaryPath).WithArguments(arguments ?? []);

            if (!string.IsNullOrEmpty(workingDirectory)) 
            {
                command = command.WithWorkingDirectory(workingDirectory);
            }
            if (!throwOnNonZeroExitCode)
            {
                command = command.WithValidation(CommandResultValidation.None);
            }

            // Prefer not to listen. Some processes hang if we capture their output even after all input has been collected
            //
            if (outputListener == null && errorListener == null)
            {
                var result = await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                return new ExecuteResult(result.ExitCode);
            }

            int? exitCode = null;
            await foreach (var cmdEvent in command.ListenAsync(cancellationToken))
            {
                switch (cmdEvent)
                {
                    case StandardOutputCommandEvent stdOut:
                        if (outputListener != null)
                        {
                            await outputListener(stdOut.Text, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    case StandardErrorCommandEvent stdErr:
                        if (errorListener != null)
                        {
                            await errorListener(stdErr.Text, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    case ExitedCommandEvent exited:
                        logger.LogDebug($"{binaryPath} {string.Join(' ', arguments)} finished with exit code {exited.ExitCode}");
                        exitCode = exited.ExitCode;
                        break;
                }

                if (exitCode != null)
                {
                    break;
                }
            }

            return new ExecuteResult(exitCode.Value);
        }
        catch (CommandExecutionException ex)
        {
            throw new EmbeddedPostgresCommandExecutionException(ex.ExitCode, $"{binaryPath} failed with error [{ex.ExitCode}]: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new EmbeddedPostgresCommandExecutionException(-1, $"{binaryPath} failed with error : {ex.Message}");
        }
    }
}
