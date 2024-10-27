using EmbeddedPostgres.Infrastructure.Interfaces;

namespace EmbeddedPostgres.Infrastructure.Interfaces;

/// <summary>
/// Defines a factory for creating <see cref="IFileExtractor"/> instances based on file type or extraction strategy.
/// </summary>
public interface IFileExtractorFactory
{
    /// <summary>
    /// Creates an <see cref="IFileExtractor"/> for the specified file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension for which to create an extractor (e.g., ".zip", ".tar").</param>
    /// <returns>
    /// An instance of <see cref="IFileExtractor"/> that is capable of extracting files of the specified type.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileExtension"/> is <c>null</c> or empty.
    /// </exception>
    IFileExtractor ForFileType(string fileExtension);

    /// <summary>
    /// Creates an <see cref="IFileExtractor"/> for the specified extraction strategy.
    /// </summary>
    /// <param name="scheme">The extraction strategy scheme (e.g., "sharp", "system", "zonkio"). 
    /// See <see cref="KnownExtractionStrategies"/></param>
    /// <returns>
    /// An instance of <see cref="IFileExtractor"/> that implements the specified extraction strategy.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scheme"/> is <c>null</c> or empty.
    /// </exception>
    IFileExtractor ForExtractionStrategy(string scheme);
}
