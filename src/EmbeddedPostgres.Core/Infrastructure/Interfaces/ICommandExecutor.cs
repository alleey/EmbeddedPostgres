using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

/// <summary>
/// Represents the result of an executed command, including the exit code and success status.
/// </summary>
public class ExecuteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteResult"/> class.
    /// </summary>
    /// <param name="exitCode">The exit code set by the underlying process.</param>
    public ExecuteResult(int exitCode)
    {
        ExitCode = exitCode;
    }

    /// <summary>
    /// Exit code set by the underlying process.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Indicates whether the command execution was successful (i.e., exit code is zero).
    /// </summary>
    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Defines a contract for executing commands asynchronously.
/// </summary>
public interface ICommandExecutor
{
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
    Task<ExecuteResult> ExecuteAsync(
        string binaryPath,
        IEnumerable<string> arguments = default,
        IReadOnlyDictionary<string, string> environmentVariables = null,
        string workingDirectory = default,
        bool validateNonZeroExitCode = true,
        Func<string, CancellationToken, Task> outputListener = default,
        Func<string, CancellationToken, Task> errorListener = default,
        CancellationToken cancellationToken = default);
}
