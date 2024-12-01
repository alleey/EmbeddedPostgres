using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines an interface for dumping PostgreSQL clusters with specific dump options and configurations.
/// Extends <see cref="IPgExecutableController"/> for common execution-related functionality.
/// </summary>
public interface IPgDumpController : IPgExecutableController
{
    /// <summary>
    /// Dumps a PostgreSQL data cluster based on the provided configuration and dump options.
    /// </summary>
    /// <param name="dataCluster">The configuration of the PostgreSQL data cluster to be restored.</param>
    /// <param name="options">Options specifying the details of the dump process.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the dump process to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous dump operation.</returns>
    Task DumpAsync(PgDataClusterConfiguration dataCluster, PgExportDumpOptions options, CancellationToken cancellationToken = default);
}