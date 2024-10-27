using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines an interface for initializing a PostgreSQL data cluster. 
/// Classes implementing this interface provide the logic for setting up or restoring 
/// a PostgreSQL cluster based on the provided configuration and options.
/// </summary>
public interface IPgClusterInitializer
{
    /// <summary>
    /// Asynchronously initializes the PostgreSQL data cluster using the provided configuration.
    /// Implementations may perform various actions such as setting up a new data cluster, 
    /// restoring from a backup, or initializing the cluster from scratch.
    /// </summary>
    /// <param name="dataCluster">The configuration object representing the PostgreSQL data cluster to initialize.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing the initialization process to be canceled if necessary.
    /// Defaults to <see cref="CancellationToken.None"/> if not provided.
    /// </param>
    /// <returns>A task that represents the asynchronous operation for initializing the data cluster.</returns>
    Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default);
}
