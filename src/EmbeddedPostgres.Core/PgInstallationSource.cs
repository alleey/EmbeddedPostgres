using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbeddedPostgres;

/// <summary>
/// Represents a source for PostgreSQL installation artifacts.
/// This class allows you to specify main binaries and extensions for the PostgreSQL installation.
/// </summary>
public class PgInstallationSource
{
    private readonly List<PgArtifact> artifacts = new();
    private readonly string cacheDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgInstallationSource"/> class.
    /// </summary>
    /// <param name="cacheDirectory">
    /// The directory where downloaded artifacts will be cached.
    /// Must not be null or whitespace.
    /// </param>
    public PgInstallationSource(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory, nameof(cacheDirectory));
        this.cacheDirectory = cacheDirectory;
    }

    /// <summary>
    /// Downloads the main artifact from the web. 
    /// Use the <see cref="PgStandardBinaries"/> or <see cref="PgZonkyioBinaries"/> to provide artifacts.
    /// </summary>
    /// <param name="pgMainBinaries">
    /// The main PostgreSQL binaries artifact to download.
    /// Must not be null, and its <c>Source</c> property must not be null or whitespace.
    /// </param>
    /// <returns>
    /// The current <see cref="PgInstallationSource"/> instance for method chaining.
    /// </returns>
    public PgInstallationSource UseMain(PgArtifact pgMainBinaries)
    {
        ArgumentNullException.ThrowIfNull(pgMainBinaries, nameof(pgMainBinaries));
        ArgumentException.ThrowIfNullOrWhiteSpace(pgMainBinaries.Source, nameof(pgMainBinaries.Source));
        RequireOnlyOneMainArtifact();
        RequireUniqueArtifact(pgMainBinaries.Source);

        artifacts.Add(
            pgMainBinaries with
            {
                Kind = PgArtifactKind.Main,
                Target = cacheDirectory,
            }
        );

        return this;
    }

    /// <summary>
    /// Uses a local binaries archive as the main PostgreSQL extension.
    /// </summary>
    /// <param name="pgExtension">
    /// The PostgreSQL extension artifact to use. Must not be null, and its <c>Source</c> property must not be null or whitespace.
    /// </param>
    /// <returns>
    /// The current <see cref="PgInstallationSource"/> instance for method chaining.
    /// </returns>
    public PgInstallationSource UseExtension(PgArtifact pgExtension)
    {
        ArgumentNullException.ThrowIfNull(pgExtension, nameof(pgExtension));
        ArgumentException.ThrowIfNullOrWhiteSpace(pgExtension.Source, nameof(pgExtension.Source));
        RequireUniqueArtifact(pgExtension.Source);

        artifacts.Add(
            pgExtension with
            {
                Kind = PgArtifactKind.Extension,
                Target = cacheDirectory,
            }
        );

        return this;
    }

    /// <summary>
    /// Builds the collection of artifacts to be fed to the <see cref="IPgInstanceBuilder"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable{PgArtifact}"/> containing the specified artifacts.
    /// </returns>
    public IEnumerable<PgArtifact> Build() => artifacts;

    /// <summary>
    /// Ensures that the specified download URL is unique among the artifacts.
    /// Throws an exception if the URL is already specified.
    /// </summary>
    /// <param name="downloadUrl">
    /// The download URL to check for uniqueness.
    /// </param>
    /// <exception cref="PgCoreException">
    /// Thrown when the download URL is already specified.
    /// </exception>
    private void RequireUniqueArtifact(string downloadUrl)
    {
        if (artifacts.Any(item => item.Source == downloadUrl))
        {
            throw new PgCoreException($"{downloadUrl} is already specified");
        }
    }

    /// <summary>
    /// Ensures that only one main artifact is specified.
    /// Throws an exception if more than one main artifact is present.
    /// </summary>
    /// <exception cref="PgCoreException">
    /// Thrown when more than one main download link is specified.
    /// </exception>
    private void RequireOnlyOneMainArtifact()
    {
        if (artifacts.Any(item => item.Kind == PgArtifactKind.Main))
        {
            throw new PgCoreException("Cannot have more than one main download links");
        }
    }
}
