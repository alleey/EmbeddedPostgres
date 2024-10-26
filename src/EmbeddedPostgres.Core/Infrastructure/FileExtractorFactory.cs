using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace EmbeddedPostgres.Infrastructure;

internal class FileExtractorFactory : IFileExtractorFactory
{
    private readonly IFileExtractor systemFileExtractor;
    private readonly IFileExtractor sharpCompressExtractor;
    private readonly IFileExtractor zonkyExtractor;

    public FileExtractorFactory(
        [FromKeyedServices(KnownExtractionStrategies.System)] IFileExtractor systemFileExtractor,
        [FromKeyedServices(KnownExtractionStrategies.Sharp)] IFileExtractor sharpCompressExtractor,
        [FromKeyedServices(KnownExtractionStrategies.Zonky)] IFileExtractor zonkyExtractor)
    {
        this.systemFileExtractor = systemFileExtractor;
        this.sharpCompressExtractor = sharpCompressExtractor;
        this.zonkyExtractor = zonkyExtractor;
    }

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
    public IFileExtractor ForFileType(string fileNameOrExtension)
    {
        return Path.GetExtension(fileNameOrExtension).ToLower() switch
        {
            ".jar" => systemFileExtractor,
            _ => sharpCompressExtractor
        };
    }

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
    public IFileExtractor ForExtractionStrategy(string scheme)
    {
        return scheme.ToLower() switch
        {
            KnownExtractionStrategies.Zonky => zonkyExtractor,
            KnownExtractionStrategies.System => systemFileExtractor,
            _ => sharpCompressExtractor
        };
    }
}