using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

public interface IPgDataClusterController : IPgExecutableController
{
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
    Task<PgRuntimeStatus> GetStatusAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default);

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
    Task StartAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default);

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
    Task StopAsync(PgDataClusterConfiguration dataCluster, PgShutdownParams shutdownParams, CancellationToken cancellationToken = default);

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
    Task RestartAsync(PgDataClusterConfiguration dataCluster, PgShutdownParams shutdownParams, CancellationToken cancellationToken = default);

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
    Task ReloadConfigurationAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default);
}
