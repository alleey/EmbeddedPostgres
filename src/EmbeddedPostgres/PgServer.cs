using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Extensions;

namespace EmbeddedPostgres;

/// <summary>
/// Represents an event that occurs for a PostgreSQL data cluster.
/// </summary>
/// <param name="dataCluster">The associated data cluster for the event.</param>
public record PgDataClusterEvent(PgDataCluster dataCluster, Exception errorInfo = null)
{
    /// <summary>
    /// Gets the data cluster associated with the event.
    /// </summary>
    public PgDataCluster DataCluster { get; init; } = dataCluster;

    /// <summary>
    /// Gets or sets the error information, if the event resulted in an error.
    /// </summary>
    public Exception ErrorInfo { get; init; } = errorInfo;

    /// <summary>
    /// Indicates whether the event was successful.
    /// </summary>
    public bool IsSuccess => ErrorInfo == null;

    /// <summary>
    /// Indicates whether the event resulted in a failure.
    /// </summary>
    public bool IsFailure => ErrorInfo != null;
}

/// <summary>
/// Event indicating that a data cluster has been initialized.
/// </summary>
public record PgDataClusterInitialzedEvent(PgDataCluster dataCluster, Exception errorInfo = null) : PgDataClusterEvent(dataCluster, errorInfo);

/// <summary>
/// Event indicating that a data cluster has started.
/// </summary>
public record PgDataClusterStartedEvent(PgDataCluster dataCluster, Exception errorInfo = null) : PgDataClusterEvent(dataCluster, errorInfo);

/// <summary>
/// Event indicating that a data cluster has stopped.
/// </summary>
public record PgDataClusterStopEvent(PgDataCluster dataCluster, Exception errorInfo = null) : PgDataClusterEvent(dataCluster, errorInfo);

/// <summary>
/// Event indicating that a data cluster has reloaded its configuration.
/// </summary>
public record PgDataClusterReloadEvent(PgDataCluster dataCluster, Exception errorInfo = null) : PgDataClusterEvent(dataCluster, errorInfo);

/// <summary>
/// Manages PostgreSQL data clusters, including initialization, starting, stopping, and reloading configuration.
/// </summary>
public class PgServer
{
    /// <summary>
    /// Represents all data clusters.
    /// </summary>
    public static readonly IEnumerable<string> AllDataClusters = Enumerable.Empty<string>();

    private readonly object synchronizationLock = new object();
    private readonly PgEnvironment environment;
    private readonly Dictionary<string, PgDataCluster> dataClusters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PgServer"/> class with the specified environment.
    /// </summary>
    /// <param name="environment">The environment containing configuration for data clusters.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided environment is null.</exception>
    public PgServer(PgEnvironment environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        foreach (var cluster in environment.DataClusters)
        {
            AddDataCluster(cluster);
        }
    }

    /// <summary>
    /// Gets the environment associated with the server.
    /// </summary>
    public PgEnvironment Environment => environment;

    /// <summary>
    /// Gets the collection of all configured data clusters.
    /// </summary>
    public IReadOnlyCollection<PgDataCluster> DataClusters => dataClusters.Values.ToList().AsReadOnly();

    /// <summary>
    /// Indicates whether the server is in minimal mode.
    /// </summary>
    public bool IsMinimal => environment.SqlController == null;

    /// <summary>
    /// Adds a data cluster configuration to the server.
    /// </summary>
    /// <param name="configuration">The configuration for the data cluster to add.</param>
    /// <exception cref="ArgumentException">Thrown when a data cluster with the same data directory already exists.</exception>
    public void AddDataCluster(PgDataClusterConfiguration configuration)
    {
        lock (synchronizationLock)
        {
            if (dataClusters.ContainsKey(configuration.DataDirectory))
            {
                throw new ArgumentException($"Data cluster for {configuration.DataDirectory} already exists.");
            }
            dataClusters[configuration.UniqueId] = new PgDataCluster(environment, configuration);
        }
    }

    /// <summary>
    /// Retrieves a data cluster by its unique identifier.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the data cluster to retrieve.</param>
    /// <returns>The data cluster associated with the given identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when no data cluster is found with the provided identifier.</exception>
    public PgDataCluster GetClusterByUniqueId(string clusterId)
    {
        if (!dataClusters.TryGetValue(clusterId, out var cluster))
        {
            throw new ArgumentException($"Data cluster having {clusterId} not found.");
        }
        return cluster;
    }

    /// <summary>
    /// Initializes the specified data clusters asynchronously.
    /// </summary>
    /// <param name="clusters">Optional. A collection of cluster unique IDs to initialize. If null, all clusters are initialized.</param>
    /// <param name="initializer">Optional. A function to provide the initialization source for each cluster.</param>
    /// <param name="maxDegreeOfParallelism">Optional. The maximum degree of parallelism for initialization tasks.</param>
    /// <param name="eventListener">Optional. A callback to handle events related to initialization.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public Task InitializeAsync(
        IEnumerable<string> clusters = null,
        Func<PgDataCluster, IPgClusterInitializer> initializer = null,
        int maxDegreeOfParallelism = 1,
        Func<PgDataClusterInitialzedEvent, CancellationToken, Task> eventListener = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (initializer == null)
        {
            initializer = cluster => new PgClusterInitializerFactory(environment).InitializeUsingInitDb(PgInitDbOptions.Default);
        }

        return SelectDataClusters(clusters).ParallelForEachAsync(
            async dataCluster =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception errorInfo = null;

                try
                {
                    var initializationSource = initializer(dataCluster);
                    await dataCluster.InitializeAsync(initializationSource, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorInfo = ex;
                }

                if (eventListener != null)
                {
                    await eventListener(
                        new PgDataClusterInitialzedEvent(dataCluster, errorInfo), 
                        cancellationToken
                    ).ConfigureAwait(false);
                }
            },
            maxDop: maxDegreeOfParallelism,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Starts the specified data clusters asynchronously.
    /// </summary>
    /// <param name="clusters">Optional. A collection of cluster unique IDs to start. If null, all clusters are started.</param>
    /// <param name="startupParams">Optional. The parameters to use when starting the clusters.</param>
    /// <param name="maxDegreeOfParallelism">Optional. The maximum degree of parallelism for starting tasks.</param>
    /// <param name="eventListener">Optional. A callback to handle events related to cluster start.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public Task StartAsync(
        IEnumerable<string> clusters = null,
        PgStartupParams startupParams = null,
        Func<PgDataCluster, IPgClusterInitializer> initializer = null,
        int maxDegreeOfParallelism = 1,
        Func<PgDataClusterStartedEvent, CancellationToken, Task> eventListener = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return SelectDataClusters(clusters).ParallelForEachAsync(
            async dataCluster =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception errorInfo = null;

                try
                {
                    await dataCluster.StartAsync(startupParams, initializer?.Invoke(dataCluster), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorInfo = ex;
                }

                if (eventListener != null)
                {
                    await eventListener(
                        new PgDataClusterStartedEvent(dataCluster, errorInfo),
                        cancellationToken
                    ).ConfigureAwait(false);
                }
            },
            maxDop: maxDegreeOfParallelism,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stops the specified data clusters asynchronously.
    /// </summary>
    /// <param name="clusters">Optional. A collection of cluster unique IDs to stop. If null, all clusters are stopped.</param>
    /// <param name="shutdownParams">Optional. The parameters to use when shutting down the clusters.</param>
    /// <param name="maxDegreeOfParallelism">Optional. The maximum degree of parallelism for stopping tasks.</param>
    /// <param name="eventListener">Optional. A callback to handle events related to cluster stop.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public Task StopAsync(
        IEnumerable<string> clusters = null,
        PgShutdownParams shutdownParams = null,
        int maxDegreeOfParallelism = 1,
        Func<PgDataClusterStopEvent, CancellationToken, Task> eventListener = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return SelectDataClusters(clusters).ParallelForEachAsync(
            async dataCluster =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception errorInfo = null;

                try
                {
                    await dataCluster.StopAsync(shutdownParams, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorInfo = ex;
                }

                if (eventListener != null)
                {
                    await eventListener(
                        new PgDataClusterStopEvent(dataCluster, errorInfo),
                        cancellationToken
                    ).ConfigureAwait(false);
                }
            },
            maxDop: maxDegreeOfParallelism,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reloads the configuration of the specified data clusters asynchronously.
    /// </summary>
    /// <param name="clusters">Optional. A collection of cluster unique IDs to reload. If null, all clusters are reloaded.</param>
    /// <param name="maxDegreeOfParallelism">Optional. The maximum degree of parallelism for reload tasks.</param>
    /// <param name="eventListener">Optional. A callback to handle events related to configuration reload.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    public Task ReloadConfigurationAsync(
        IEnumerable<string> clusters = null,
        int maxDegreeOfParallelism = 1,
        Func<PgDataClusterReloadEvent, CancellationToken, Task> eventListener = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return SelectDataClusters(clusters).ParallelForEachAsync(
            async dataCluster =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception errorInfo = null;

                try
                {
                    await dataCluster.ReloadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorInfo = ex;
                }

                if (eventListener != null)
                {
                    await eventListener(
                        new PgDataClusterReloadEvent(dataCluster, errorInfo),
                        cancellationToken
                    ).ConfigureAwait(false);
                }
            },
            maxDop: maxDegreeOfParallelism,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously archives a PostgreSQL data cluster by stopping it and compressing its data directory into a specified archive file.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster.</param>
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
        string clusterId,
        string archiveFilePath,
        PgShutdownParams shutdownParams = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetClusterByUniqueId(clusterId).ArchiveAsync(archiveFilePath, shutdownParams, cancellationToken);
    }

    /// <summary>
    /// Restores a PostgreSQL data cluster identified by the specified cluster ID based on the provided restore options.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster to be restored.</param>
    /// <param name="options">The options specifying the details of the restore process, including source paths and restore parameters.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the restore process to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous restore operation.</returns>
    public Task ImportDumpAsync(
        string clusterId,
        PgRestoreDumpOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetClusterByUniqueId(clusterId).ImportDumpAsync(options, cancellationToken);
    }

    /// <summary>
    /// Exports a PostgreSQL data cluster identified by the specified cluster ID based on the provided dump options.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster to be exported.</param>
    /// <param name="options">The options specifying the details of the export process, including destination paths and export parameters.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the export process to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous export operation.</returns>
    public Task ExportDumpAsync(
        string clusterId,
        PgExportDumpOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetClusterByUniqueId(clusterId).ExportDumpAsync(options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously lists all databases in the specified PostgreSQL data cluster and invokes a listener 
    /// for each database found.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster.</param>
    /// <param name="listener">A callback function that is invoked for each <see cref="PgDatabaseInfo"/> 
    /// instance representing a database in the cluster.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an 
    /// <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="clusterId"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the 
    /// <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method retrieves the specified cluster by its unique identifier and invokes the listener
    /// for each database in that cluster. The listener receives a <see cref="PgDatabaseInfo"/> object
    /// containing details about each database.
    /// </remarks>
    public Task ListDatabasesAsync(
        string clusterId,
        Func<PgDatabaseInfo, CancellationToken, Task> listener,
        CancellationToken cancellationToken = default) 
        => GetClusterByUniqueId(clusterId).ListDatabasesAsync(listener, cancellationToken);

    /// <summary>
    /// Asynchronously executes the specified SQL command against a PostgreSQL data cluster's database.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster.</param>
    /// <param name="sql">The SQL command to be executed.</param>
    /// <param name="databaseName">The name of the database against which to execute the SQL command.
    /// If not provided, the default database will be used.</param>
    /// <param name="userName">The username under which to execute the SQL command. If not provided, 
    /// the default user will be used.</param>
    /// <param name="listener">A callback function that is invoked with the result of the SQL command execution.</param>
    /// <param name="format">The format for the SQL result. This can be used to specify how results 
    /// should be formatted.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an 
    /// <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="clusterId"/> or <paramref name="sql"/> 
    /// is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the 
    /// <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method retrieves the specified cluster by its unique identifier and executes the provided 
    /// SQL command against the specified database. The listener function is invoked with the results 
    /// of the execution.
    /// </remarks>
    public Task ExecuteSqlAsync(
        string clusterId,
        string sql,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default
        ) => GetClusterByUniqueId(clusterId).ExecuteSqlAsync(sql,
                                                             databaseName,
                                                             userName,
                                                             listener,
                                                             format,
                                                             cancellationToken: cancellationToken);

    /// <summary>
    /// Asynchronously executes SQL commands from a specified file against a PostgreSQL data cluster's database.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the PostgreSQL data cluster.</param>
    /// <param name="filePath">The path to the file containing the SQL commands to be executed.</param>
    /// <param name="databaseName">The name of the database against which to execute the SQL commands.
    /// If not provided, the default database will be used.</param>
    /// <param name="userName">The username under which to execute the SQL commands. If not provided, 
    /// the default user will be used.</param>
    /// <param name="listener">A callback function that is invoked with the result of the SQL command execution.</param>
    /// <param name="format">The format for the SQL result. This can be used to specify how results 
    /// should be formatted.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation. If the token is canceled, an 
    /// <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="clusterId"/> or <paramref name="filePath"/> 
    /// is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled through the 
    /// <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method retrieves the specified cluster by its unique identifier and executes the SQL commands
    /// found in the specified file against the specified database. The listener function is invoked with
    /// the results of the execution.
    /// </remarks>
    public Task ExecuteFileAsync(
        string clusterId,
        string filePath,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default
        ) => GetClusterByUniqueId(clusterId).ExecuteFileAsync(filePath,
                                                              databaseName,
                                                              userName,
                                                              listener,
                                                              format,
                                                              cancellationToken: cancellationToken);

    /// <summary>
    /// Selects data clusters based on the specified cluster identifiers.
    /// </summary>
    /// <param name="clusters">Optional. A collection of cluster unique IDs to select. If null or empty, all clusters are selected.</param>
    /// <returns>A collection of selected data clusters.</returns>
    private IEnumerable<PgDataCluster> SelectDataClusters(IEnumerable<string> clusters)
    {
        IEnumerable<PgDataCluster> selectedClusters;

        lock (synchronizationLock)
        {
            selectedClusters = dataClusters.Values.ToList();
        }

        // If clusters are specified, filter by the provided cluster IDs
        return clusters?.Any() == true
            ? selectedClusters.Where(x => clusters.Contains(x.UniqueId))
            : selectedClusters;
    }
}
