using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Controllers;

internal class PgDumpController : IPgDumpController
{
    private readonly PgInstanceConfiguration instance;
    private readonly string pgDumpPath;
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;
    private readonly ILogger<PgDumpController> logger;

    public PgDumpController(
        string pgDumpePathOrFilename,
        PgInstanceConfiguration instance,
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor,
        ILogger<PgDumpController> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pgDumpePathOrFilename, nameof(pgDumpePathOrFilename));

        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // An absolute path is used if provided
        this.pgDumpPath = Path.Combine(Path.GetFullPath(Path.Combine(instance.InstanceDirectory, "bin")), pgDumpePathOrFilename);
    }

    public PgInstanceConfiguration Instance => instance;

    /// <summary>
    /// Retrieves the initdb version asynchronously.
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
    public Task<string> GetVersionAsync(bool noThrow = true, CancellationToken cancellationToken = default)
        => this.GetVersionAsync(pgDumpPath, commandExecutor, noThrow, cancellationToken);

    /// <summary>
    /// Dumps a PostgreSQL data cluster based on the provided configuration and dump options.
    /// </summary>
    /// <param name="dataCluster">The configuration of the PostgreSQL data cluster to be restored.</param>
    /// <param name="options">Options specifying the details of the dump process.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the restore process to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous restore operation.</returns>
    public async Task DumpAsync(
        PgDataClusterConfiguration dataCluster,
        PgExportDumpOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        options.Validate();

        var args = BuildArguments(dataCluster, options);
        var env = new Dictionary<string, string> { };

        if (options.RunAsPassword != null)
        {
            env[KnownEnvironmentVariables.Password] = options.RunAsPassword;
        }

        try
        {
            await commandExecutor.ExecuteAsync(
                pgDumpPath,
                args,
                environmentVariables: env,
                errorListener: (line, ct) =>
                {
                    logger.LogError(line);
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException(ex.Message);
        }

        List<string> BuildArguments(PgDataClusterConfiguration dataCluster, PgExportDumpOptions options)
        {
            List<string> args = [
                "-U", string.IsNullOrEmpty(options.RunAsUser) ? dataCluster.Superuser : options.RunAsUser,
                "-h", dataCluster.Host,
                "-p", $"{dataCluster.Port}",
            ];
            args.AddRange(options.Build());
            return args;
        }
    }
}
