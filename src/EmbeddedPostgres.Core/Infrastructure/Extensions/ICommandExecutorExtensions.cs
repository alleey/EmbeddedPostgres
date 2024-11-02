using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="ICommandExecutor"/> interface.
/// </summary>
public static class ICommandExecutorExtensions
{
    /// <summary>
    /// Executes a command asynchronously using the specified binary path and arguments.
    /// </summary>
    /// <param name="instance">The instance of <see cref="ICommandExecutor"/> to extend.</param>
    /// <param name="binaryPath">The path to the executable binary to run.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="workingDirectory">The working directory from which to execute the command.</param>
    /// <param name="throwOnNonZeroExitCode">Indicates whether to throw an exception if the command returns a non-zero exit code.</param>
    /// <param name="outputListener">An optional action to handle standard output from the command.</param>
    /// <param name="errorListener">An optional action to handle standard error from the command.</param>
    /// <param name="cancellationToken">A token for cancellation of the operation.</param>
    /// <returns>A <see cref="Task{ExecuteResult}"/> representing the asynchronous operation. 
    /// The task result contains the execution result, including exit code and output.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> or <paramref name="binaryPath"/> is null.</exception>
    public static Task<ExecuteResult> ExecuteAsync(
        this ICommandExecutor instance,
        string binaryPath,
        IEnumerable<string> arguments = default,
        IReadOnlyDictionary<string, string> environmentVariables = null,
        string workingDirectory = default,
        bool throwOnNonZeroExitCode = true,
        Action<string> outputListener = default,
        Action<string> errorListener = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Convert Action<string> to Func<string, CancellationToken, Task>
        Func<string, CancellationToken, Task> asyncOutputListener = outputListener != null
            ? (text, token) =>
            {
                outputListener(text);
                return Task.CompletedTask;
            }
        : null;

        // Convert Action<string> to Func<string, CancellationToken, Task>
        Func<string, CancellationToken, Task> asyncErrorListener = errorListener != null
            ? (text, token) =>
            {
                errorListener(text);
                return Task.CompletedTask;
            }
        : null;

        return instance.ExecuteAsync(
            binaryPath,
            arguments,
            environmentVariables,
            workingDirectory,
            throwOnNonZeroExitCode,
            asyncOutputListener,
            asyncErrorListener,
            cancellationToken);
    }
}