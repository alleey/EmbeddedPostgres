using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres;

internal class PgDataClusterController : IPgDataClusterController
{
    private readonly PgInstanceConfiguration instance;
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;
    private readonly string pgctlPath;

    public PgDataClusterController(
        string pgctlPathOrFilename,
        PgInstanceConfiguration instance,
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor)
    {
        this.instance = instance;
        this.fileSystem = fileSystem;
        this.commandExecutor = commandExecutor;
        this.pgctlPath = Path.Combine(Path.GetFullPath(Path.Combine(instance.InstanceDirectory, "bin")), pgctlPathOrFilename);
    }

    /// <summary>
    /// 
    /// </summary>
    public PgInstanceConfiguration Instance => instance;

    /// <summary>
    /// Retrieves the pg_ctl version asynchronously.
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
    public Task<string> GetVersionAsync(
        bool noThrow = true,
        CancellationToken cancellationToken = default)
        => this.GetVersionAsync(pgctlPath, commandExecutor, noThrow, cancellationToken);

    /// <summary>
    /// Returns the process ID (PID) of the PostgreSQL server if it is currently running.
    /// Returns 0 if the server is not running.
    /// </summary>
    /// <param name="dataDirectory">
    /// The path to the data directory for the PostgreSQL instance. 
    /// If null, the default data directory is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the status check to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task{PgRuntimeStatus}"/> representing the asynchronous operation, 
    /// which contains the runtime status including the PID.
    /// </returns>
    public async Task<PgRuntimeStatus> GetStatusAsync(
        PgDataClusterConfiguration dataCluster,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dataDirectoryPath = instance.GetDataFullPath(dataCluster);
        string[] args = [
            "status",
            "-D", dataDirectoryPath,
        ];

        ExecuteResult result;
        try
        {
            result = await commandExecutor.ExecuteAsync(
                pgctlPath,
                args,
                validateNonZeroExitCode: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                var postmasterFile = Path.Combine(dataDirectoryPath, "postmaster.pid");
                if (fileSystem.FileExists(postmasterFile))
                {
                    return await ReadPostmasterPidAsync(fileSystem.Open(postmasterFile)).ConfigureAwait(false);
                }
            }
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{pgctlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }
        catch (Exception ex)
        {
            throw new PgCoreException($"Getting postgres status failed: {ex.Message}");
        }

        return PgRuntimeStatus.Invalid with { StatusError = result.ExitCode };

        ///
        async Task<PgRuntimeStatus> ReadPostmasterPidAsync(Stream stream)
        {
            using var reader = new StreamReader(stream);

            int.TryParse(await reader.ReadLineAsync().ConfigureAwait(false), out var pid);
            // PID

            var dataDirectory = await reader.ReadLineAsync().ConfigureAwait(false);
            // Data directory path

            long.TryParse(await reader.ReadLineAsync().ConfigureAwait(false), out var startTime);
            // System start time in seconds

            int.TryParse(await reader.ReadLineAsync().ConfigureAwait(false), out var port);
            // Port number

            var host = await reader.ReadLineAsync().ConfigureAwait(false);
            // Host

            return new PgRuntimeStatus
            {
                Pid = pid,
                DataDirectory = dataDirectory,
                StartTime = startTime,
                Port = port,
                Host = host
            };
        }
    }

    /// <summary>
    /// Starts the PostgreSQL server associated with the specified data directory.
    /// If the server is already running, this method may have no effect or throw an exception,
    /// depending on the implementation.
    /// </summary>
    /// <param name="dataDirectory">
    /// The path to the data directory for the PostgreSQL instance.
    /// If null, the default data directory is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the start operation to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task StartAsync(
        PgDataClusterConfiguration dataCluster,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments();

        try
        {
            await commandExecutor.ExecuteAsync(
                pgctlPath,
                args,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{pgctlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        string[] BuildArguments()
        {
            List<string> postgresOptions = [
                "-F", // run without fsync
                "-p", $"{dataCluster.Port}",
            ];

            foreach (var item in dataCluster.Parameters)
            {
                postgresOptions.Add($"-c {item.Key}={item.Value}");
            }

            string[] args = [
                "start",
                "-U", dataCluster.Superuser,
                "-D", instance.GetDataFullPath(dataCluster),
                "-o", string.Join(" ", postgresOptions)
            ];

            return args;
        }
    }

    /// <summary>
    /// Stops the PostgreSQL server gracefully using the specified shutdown parameters.
    /// The behavior of the shutdown depends on the parameters provided.
    /// </summary>
    /// <param name="shutdownParams">
    /// The parameters to control the shutdown behavior (e.g., immediate or smart shutdown).
    /// </param>
    /// <param name="dataDirectory">
    /// The path to the data directory for the PostgreSQL instance.
    /// If null, the default data directory is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the stop operation to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task StopAsync(
        PgDataClusterConfiguration dataCluster,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments(shutdownParams ?? PgShutdownParams.Default);

        try
        {
            await commandExecutor.ExecuteAsync(pgctlPath, args, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{pgctlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        string[] BuildArguments(PgShutdownParams shutdownParams)
        {
            return [
                "stop",
                "-U", dataCluster.Superuser,
                "-D", instance.GetDataFullPath(dataCluster),
                "-m", shutdownParams.Mode.ToString().ToLower(),
                shutdownParams.Wait ? "--wait" : "--no-wait",
                "-t", $"{shutdownParams.WaitTimeoutSecs}"
            ];
        }
    }

    /// <summary>
    /// Restarts the PostgreSQL server using the specified shutdown parameters.
    /// This method stops the server and then starts it again.
    /// </summary>
    /// <param name="shutdownParams">
    /// The parameters to control the shutdown behavior during the restart.
    /// </param>
    /// <param name="dataDirectory">
    /// The path to the data directory for the PostgreSQL instance.
    /// If null, the default data directory is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the restart operation to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task RestartAsync(
        PgDataClusterConfiguration dataCluster,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments(shutdownParams ?? PgShutdownParams.Default);

        try
        {
            await commandExecutor.ExecuteAsync(pgctlPath, args, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{pgctlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        string[] BuildArguments(PgShutdownParams shutdownParams)
        {
            return [
                "restart",
                "-U", dataCluster.Superuser,
                "-D", instance.GetDataFullPath(dataCluster),
                "-m", shutdownParams.Mode.ToString().ToLower(),
                shutdownParams.Wait ? "--wait" : "--no-wait",
                "-t", $"{shutdownParams.WaitTimeoutSecs}"
            ];
        }
    }

    /// <summary>
    /// Reloads the PostgreSQL server configuration without restarting the server.
    /// This allows changes made to configuration files to take effect immediately.
    /// </summary>
    /// <param name="dataDirectory">
    /// The path to the data directory for the PostgreSQL instance.
    /// If null, the default data directory is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the reload operation to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task ReloadConfigurationAsync(
        PgDataClusterConfiguration dataCluster,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments();

        try
        {
            await commandExecutor.ExecuteAsync(pgctlPath, args, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{pgctlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        string[] BuildArguments()
        {
            return [
                "reload",
                "-U", dataCluster.Superuser,
                "-D", instance.GetDataFullPath(dataCluster),
            ];
        }
    }

    /// <summary>
    /// Asynchronously destroys a PostgreSQL data cluster by stopping it and deleting its associated data directory.
    /// </summary>
    /// <param name="dataCluster">The configuration details of the PostgreSQL data cluster to be destroyed.</param>
    /// <param name="shutdownParams">
    /// The parameters for shutting down the PostgreSQL data cluster. If not provided, the default shutdown parameters will be used.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. It retrieves the status of the data cluster using <see cref="GetStatusAsync"/>.
    /// 2. If the data cluster is running (i.e., the status is valid), it stops the cluster using <see cref="StopAsync"/> with the provided shutdown parameters.
    /// 3. It then checks if the data directory associated with the cluster exists on the file system.
    /// 4. If the directory exists, it deletes the directory.
    /// </remarks>
    public async Task DestroyAsync(
        PgDataClusterConfiguration dataCluster,
        PgShutdownParams shutdownParams,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = await GetStatusAsync(dataCluster, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (status.IsValid)
        {
            await StopAsync(dataCluster, shutdownParams ?? PgShutdownParams.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var dataDirectory = instance.GetDataFullPath(dataCluster);
        if (fileSystem.DirectoryExists(dataDirectory))
        {
            fileSystem.DeleteDirectory(dataDirectory);
        }
    }
}
