using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines the contract for downloading and managing PostgreSQL artifacts.
/// </summary>
internal interface IPgArtifactsBuilder
{
    /// <summary>
    /// Downloads the specified artifacts if they are not already present.
    /// Note that these artifacts are downloaded to the <see cref="CacheDirectory"/>, 
    /// which may be different from the instance directory.
    /// </summary>
    /// <param name="artifacts">
    /// A collection of artifacts to download. Each artifact should specify its 
    /// source and any other necessary metadata required for the download process.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task will complete 
    /// with a collection of downloaded artifacts, including their updated metadata 
    /// after the download.
    /// </returns>
    Task<IEnumerable<PgArtifact>> BuildAsync(IEnumerable<PgArtifact> artifacts, CancellationToken cancellationToken = default);
}
