using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

/// <summary>
/// Provides HTTP-related functionalities for downloading artifacts to a specified target directory.
/// </summary>
public interface IHttpService
{
    /// <summary>
    /// Downloads a collection of artifacts to the specified target directory asynchronously.
    /// </summary>
    /// <param name="sourceUrl">URL to be downloaded.</param>
    /// <param name="destDirectory">The directory where downloaded artifacts will be saved.</param>
    /// <param name="destFilename">The name of downloaded resource. If no name is provided the name is constructed from the URL. The constructed name is 
    /// guaranteed to be valid and remains the same for the same sourceUrl.</param>
    /// <param name="force">Specifies whether to overwrite existing files in the target directory. The default value is <c>false</c>.</param>
    /// <param name="cancellationToken">
    /// An optional <see cref="CancellationToken"/> to observe while waiting for the download to complete.
    /// The default value is <see cref="CancellationToken.None"/>, which represents no cancellation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous download operation. The task result contains a collection
    /// of file paths corresponding to the successfully downloaded artifacts.
    /// </returns>
    Task<string> DownloadAsync(
        string sourceUrl,
        string destDirectory,
        string destFilename = null,
        bool force = false,
        CancellationToken cancellationToken = default);
}
