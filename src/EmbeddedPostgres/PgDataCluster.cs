using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;

namespace EmbeddedPostgres;

public class PgDataCluster
{
    private readonly PgEnvironment environment;
    private readonly PgDataClusterConfiguration dataCluster;

    public PgDataCluster(PgEnvironment environment, PgDataClusterConfiguration dataCluster)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.dataCluster = dataCluster ?? throw new ArgumentNullException(nameof(dataCluster));
    }

    public PgEnvironment Environment => environment;
    public PgDataClusterConfiguration Settings => dataCluster;
    public string UniqueId => dataCluster.UniqueId;

    /// <summary>
    /// Checks whether the PostgreSQL data cluster in the specified data directory has been initialized.
    /// The initialization is determined by the presence of the <c>PG_VERSION</c> file in the data directory.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the data cluster is initialized (i.e., the <c>PG_VERSION</c> file exists); otherwise, <c>false</c>.
    /// </returns>
    public bool IsInitialized() => environment.InitDb.IsInitialized(dataCluster);

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
    public Task<PgRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => environment.Controller.GetStatusAsync(dataCluster, cancellationToken);

    /// <summary>
    /// Initializes a PostgreSQL data cluster in the specified data directory by running the `initdb` command.
    /// If the data cluster is already initialized, the method returns without performing any action.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that allows the initialization process to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. The task completes when the data cluster initialization finishes.
    /// </returns>
    /// <exception cref="PgCoreException">
    /// Thrown if an error occurs during the execution of the `initdb` command.
    /// </exception>
    public async Task InitializeAsync(IPgInitializationSource initializationSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initializationSource);
        await initializationSource.InitializeAsync(dataCluster, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the PostgreSQL server associated with the specified data directory.
    /// If the server is already running, this method may have no effect or throw an exception,
    /// depending on the implementation.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that allows the start operation to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task StartAsync(PgStartupParams startupParams, IPgInitializationSource initializationSource = null, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.IsValid)
        {
            // Already running
            return;
        }

        if (!IsInitialized())
        {
            if (initializationSource == null)
            {
                throw new PgCoreException("Data cluster needs initializatio. Either call InitializeAsync or provide initializationSource");
            }

            await InitializeAsync(initializationSource, cancellationToken).ConfigureAwait(false);
        }

        await environment.Controller.StartAsync(dataCluster, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (startupParams.Wait)
        {
            dataCluster.WaitForStartup(startupParams.WaitTimeoutSecs * 1000);
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
    public async Task StopAsync(PgShutdownParams shutdownParams = null, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsValid)
        {
            // Not running
            return;
        }

        await environment.Controller.StopAsync(dataCluster, shutdownParams, cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public Task ReloadConfigurationAsync(CancellationToken cancellationToken = default)
        => environment.Controller.ReloadConfigurationAsync(dataCluster, cancellationToken);

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
    /// 
    /// This operation is irreversible and will permanently delete all data associated with the specified PostgreSQL data cluster.
    /// Ensure that any necessary backups are made before calling this method.
    /// </remarks>
    public Task DestroyAsync(PgShutdownParams shutdownParams, CancellationToken cancellationToken = default) 
        => environment.DestroyAsync(dataCluster, shutdownParams, cancellationToken);

    /// <summary>
    /// Asynchronously archives a PostgreSQL data cluster by stopping it and compressing its data directory into a specified archive file.
    /// </summary>
    /// <param name="dataCluster">The configuration details of the PostgreSQL data cluster to be archived.</param>
    /// <param name="archiveFilePath">
    /// The file path where the compressed archive of the data cluster will be saved. 
    /// This path must be a valid and writable location.
    /// </param>
    /// <param name="shutdownParams">
    /// The parameters for shutting down the PostgreSQL data cluster. If not provided, the default shutdown parameters will be used.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataCluster"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="archiveFilePath"/> is null or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. It stops the PostgreSQL data cluster using <see cref="StopAsync"/> with the provided shutdown parameters.
    /// 2. It retrieves the full path to the data directory associated with the specified cluster.
    /// 3. It compresses the data directory into the specified archive file using the <see cref="FileCompressor"/>.
    /// 
    /// Ensure that the archive file path provided is valid and accessible, as this method will overwrite existing files
    /// at that location without confirmation.
    /// </remarks>
    public Task ArchiveAsync(
        string archiveFilePath,
        PgShutdownParams shutdownParams = null,
        CancellationToken cancellationToken = default) 
        => environment.ArchiveAsync(dataCluster, archiveFilePath, shutdownParams, cancellationToken);

    /// <summary>
    /// Lists the PostgreSQL databases asynchronously.
    /// </summary>
    /// <param name="listener">
    /// A callback function invoked for each database in the result set. 
    /// It takes a <see cref="PgDatabaseInfo"/> object representing the database information and a <see cref="CancellationToken"/> for task cancellation.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the task if needed.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation of listing the databases.
    /// </returns>
    /// <exception cref="PgCoreException">
    /// Thrown when an error occurs during the execution of the PostgreSQL command.
    /// </exception>
    /// <remarks>
    /// Not available in minimal environments
    /// </remarks>
    public async Task ListDatabasesAsync(Func<PgDatabaseInfo, CancellationToken, Task> listener, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (environment.IsMinimal)
        {
            throw new PgCoreException("Minimal environments do not support ListDatabases");
        }

        await RequireRunningStatus(cancellationToken).ConfigureAwait(false);
        await environment.SqlClient.ListDatabasesAsync(dataCluster, listener, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a SQL command asynchronously against the specified database. 
    /// The execution can be customized by providing a database name, user name, result format, and an optional listener for processing output.
    /// </summary>
    /// <param name="sql">The SQL command to be executed.</param>
    /// <param name="databaseName">The name of the target database. If null, the default database is used.</param>
    /// <param name="userName">The user name to use for the database connection. If null, the default user is used.</param>
    /// <param name="listener">
    /// An optional listener function that is called during the file execution, allowing real-time processing of the output. 
    /// The function takes a string representing output and a <see cref="CancellationToken"/> for managing task cancellation.
    /// </param>
    /// <param name="format">
    /// Specifies the format of the result set, such as text or binary. The default is <see cref="PgSqlResultFormat"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled if necessary.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous execution operation. The task completes when the file execution finishes.
    /// </returns>
    /// <remarks>
    /// Not available in minimal environments
    /// </remarks>
    public async Task ExecuteSqlAsync(
        string sql,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (environment.IsMinimal)
        {
            throw new PgCoreException("Minimal environments do not support ExecuteSql");
        }

        await RequireRunningStatus(cancellationToken).ConfigureAwait(false);
        await environment.SqlClient.ExecuteSqlAsync(
            dataCluster,
            sql,
            databaseName,
            userName,
            listener,
            format,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a SQL file asynchronously against the specified database. 
    /// The execution can be customized by providing a database name, user name, result format, and an optional listener for processing output.
    /// </summary>
    /// <param name="filePath">The path to the SQL file that should be executed.</param>
    /// <param name="databaseName">The name of the target database. If null, the default database is used.</param>
    /// <param name="userName">The user name to use for the database connection. If null, the default user is used.</param>
    /// <param name="listener">
    /// An optional listener function that is called during the file execution, allowing real-time processing of the output. 
    /// The function takes a string representing output and a <see cref="CancellationToken"/> for managing task cancellation.
    /// </param>
    /// <param name="format">
    /// Specifies the format of the result set, such as text or binary. The default is <see cref="PgSqlResultFormat"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled if necessary.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous execution operation. The task completes when the file execution finishes.
    /// </returns>
    /// <remarks>
    /// Not available in minimal environments
    /// </remarks>
    public async Task ExecuteFileAsync(
        string filePath,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (environment.IsMinimal)
        {
            throw new PgCoreException("Minimal environments do not support ExecuteFile");
        }

        await RequireRunningStatus(cancellationToken).ConfigureAwait(false);
        await environment.SqlClient.ExecuteFileAsync(
            dataCluster,
            filePath,
            databaseName,
            userName,
            listener,
            format,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }



    private async Task RequireRunningStatus(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.IsValid)
        {
            // Already running
            return;
        }
        throw new PgCoreException("Data cluster is currently not in the running state.");
    }
}
