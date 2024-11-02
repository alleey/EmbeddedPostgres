using EmbeddedPostgres.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

/// <summary>
/// A compound initializer for PostgreSQL clusters that sequentially initializes 
/// each provided cluster initializer.
/// </summary>
internal class PgCompoundInitializer : IPgClusterInitializer
{
    // Collection of cluster initializers to be executed.
    private readonly IPgClusterInitializer[] clusterInitializers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgCompoundInitializer"/> class.
    /// </summary>
    /// <param name="environment">The PostgreSQL environment configuration.</param>
    /// <param name="clusterInitializers">A list of cluster initializers to apply sequentially.</param>
    public PgCompoundInitializer(PgEnvironment environment, params IPgClusterInitializer[] clusterInitializers)
    {
        this.clusterInitializers = clusterInitializers;
    }

    /// <summary>
    /// Asynchronously initializes a PostgreSQL data cluster using each configured 
    /// <see cref="IPgClusterInitializer"/> in sequence.
    /// </summary>
    /// <param name="dataCluster">The configuration for the data cluster to initialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default)
    {
        foreach (var clusterInitializer in clusterInitializers)
        {
            // Perform the initialization asynchronously
            await clusterInitializer.InitializeAsync(dataCluster, cancellationToken).ConfigureAwait(false);
        }
    }
}
