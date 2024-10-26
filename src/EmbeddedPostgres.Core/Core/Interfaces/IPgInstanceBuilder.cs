using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines the contract for building and managing PostgreSQL instances.
/// </summary>
public interface IPgInstanceBuilder
{
    /// <summary>
    /// Downloads and extracts the specified artifacts into the instance directory.
    /// </summary>
    /// <param name="options">
    /// The options for configuring the instance build process, including settings 
    /// related to the download and extraction of artifacts.
    /// </param>
    /// <param name="artifacts">
    /// A collection of artifacts to be downloaded and extracted for the PostgreSQL instance.
    /// These artifacts may include binaries, extensions, and other necessary files.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous build operation. The task will complete
    /// once the download and extraction of the artifacts have finished.
    /// </returns>
    Task BuildAsync(PgInstanceBuilderOptions options, IEnumerable<PgArtifact> artifacts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Destroys the contents of the specified instance directory, effectively 
    /// cleaning up all files and configurations associated with the PostgreSQL instance.
    /// </summary>
    /// <param name="options">
    /// The configuration settings for the PostgreSQL instance that should be destroyed.
    /// This includes paths and other relevant settings for the cleanup process.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default 
    /// value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous destroy operation. The task will complete
    /// once the instance directory has been cleaned up.
    /// </returns>
    Task DestroyAsync(PgInstanceConfiguration options, CancellationToken cancellationToken = default);
}
