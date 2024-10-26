using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

public interface IPgInitDbController : IPgExecutableController
{
    /// <summary>
    /// Checks whether the PostgreSQL data cluster in the specified data directory has been initialized.
    /// The initialization is determined by the presence of the <c>PG_VERSION</c> file in the data directory.
    /// </summary>
    /// <param name="dataCluster">
    /// The dataCluster object that contains data cluster settings.
    /// </param>
    /// <returns>
    /// <c>true</c> if the data cluster is initialized (i.e., the <c>PG_VERSION</c> file exists); otherwise, <c>false</c>.
    /// </returns>
    bool IsInitialized(PgDataClusterConfiguration dataCluster);

    /// <summary>
    /// Initializes a PostgreSQL data cluster in the specified data directory by running the `initdb` command.
    /// If the data cluster is already initialized, the method returns without performing any action.
    /// </summary>
    /// <param name="dataCluster">
    /// The dataCluster object that contains data cluster settings.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the initialization process to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. The task completes when the data cluster initialization finishes.
    /// </returns>
    /// <exception cref="PgCoreException">
    /// Thrown if an error occurs during the execution of the `initdb` command.
    /// </exception>
    Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default);
}
