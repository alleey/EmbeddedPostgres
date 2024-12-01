using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Defines the contract for building a PostgreSQL environment.
/// </summary>
public interface IPgEnvironmentBuilder
{

    /// <summary>
    /// Validates the specified instance directory to ensure it meets the necessary requirements
    /// for a PostgreSQL instance. This method checks the existence of required binaries and
    /// retrieves their versions, returning a dictionary with binary names as keys and version
    /// information as values.
    /// </summary>
    /// <param name="instanceDir">
    /// The directory where the PostgreSQL instance is located. This should be a valid path
    /// that the application can access.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default value
    /// is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous validation operation, containing a dictionary
    /// where keys are binary names and values are the respective version information.
    /// </returns>
    /// <exception cref="PgValidationException">
    /// Thrown if validation fails due to a missing binary or an error during version retrieval.
    /// </exception>
    Task<Dictionary<string, string>> ValidateAsync(
        string instanceDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a <see cref="PgEnvironment"/> based on the specified instance and server configurations.
    /// </summary>
    /// <param name="instanceConfig">
    /// The configuration settings for the PostgreSQL instance.
    /// </param>
    /// <param name="dataClusters">
    /// The configuration settings for the each data cluster in the instance.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default value
    /// is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous build operation. The task will contain the 
    /// constructed <see cref="PgEnvironment"/> when completed.
    /// </returns>
    Task<PgEnvironment> BuildAsync(
        PgInstanceConfiguration instanceConfig,
        CancellationToken cancellationToken = default);
}
