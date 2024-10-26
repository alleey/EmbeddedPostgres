using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Extensions;

internal static class IPgExecutableControllerExtensions
{
    /// <summary>
    /// Retrieves the binary version asynchronously.
    /// </summary>
    /// <param name="noThrow">
    /// If set to <c>true</c>, the method will not throw an exception if the version retrieval fails; 
    /// instead, it will return <c>null</c>. If set to <c>false</c>, an exception will be thrown on failure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task{String}"/> representing the asynchronous operation. 
    /// The task result contains the PostgreSQL version string, or <c>null</c> if <paramref name="noThrow"/> is <c>true</c> and the operation fails.
    /// </returns>
    public static async Task<string> GetVersionAsync(
        this IPgExecutableController controller,
        string executablePath,
        ICommandExecutor commandExecutor,
        bool noThrow = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath, nameof(executablePath));
        cancellationToken.ThrowIfCancellationRequested();

        string version = null;

        try
        {
            await commandExecutor.ExecuteAsync(
                executablePath,
                ["--version"],
                outputListener: (output, cancellationToken) =>
                {
                    version = output;
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            if (!noThrow && string.IsNullOrEmpty(version))
            {
                throw new PgCoreException("Failed to retrieve version");
            }
        }
        catch (EmbeddedPostgresCommandExecutionException ex)
        {
            if (!noThrow)
            {
                throw new PgCoreException(ex.Message, ex);
            }
        }

        return version;
    }
}
