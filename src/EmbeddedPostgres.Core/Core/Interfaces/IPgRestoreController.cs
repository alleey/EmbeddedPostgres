using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines an interface for restoring PostgreSQL clusters with specific restore options and configurations.
/// Extends <see cref="IPgExecutableController"/> for common execution-related functionality.
/// </summary>
public interface IPgRestoreController : IPgExecutableController
{
    /// <summary>
    /// Restores a PostgreSQL data cluster based on the provided configuration and restore options.
    /// </summary>
    /// <param name="dataCluster">The configuration of the PostgreSQL data cluster to be restored.</param>
    /// <param name="options">Options specifying the details of the restore process, such as source paths and restore options.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the restore process to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous restore operation.</returns>
    Task RestoreAsync(PgDataClusterConfiguration dataCluster, PgRestoreDumpOptions options, CancellationToken cancellationToken = default);
}
