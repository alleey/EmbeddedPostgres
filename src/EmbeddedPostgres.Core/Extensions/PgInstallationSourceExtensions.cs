using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Extensions;

/// <summary>
/// Provides extension methods for configuring <see cref="PgInstallationSource"/> with local and web-based PostgreSQL artifacts.
/// </summary>
public static class PgInstallationSourceExtensions
{
    /// <summary>
    /// Use a local binaries archive as the main PostgreSQL installation.
    /// </summary>
    /// <param name="source">The <see cref="PgInstallationSource"/> instance.</param>
    /// <param name="filePath">The file path of the local binaries archive.</param>
    /// <param name="extractionStrategy">
    /// Use <see cref="KnownExtractionStrategies.Zonky"/> if the artifact was downloaded from Zonky test, otherwise leave as default.
    /// </param>
    /// <returns>The updated <see cref="PgInstallationSource"/> instance.</returns>
    public static PgInstallationSource UseMain(this PgInstallationSource source, string filePath, string extractionStrategy = null)
        => source.UseMain(
            new PgArtifact
            {
                Source = filePath,
                IsLocal = true,
                ExtractionStrategy = extractionStrategy ?? KnownExtractionStrategies.Default
            }
        );

    /// <summary>
    /// Use a local Zonky test binaries archive as the main PostgreSQL installation.
    /// </summary>
    /// <param name="source">The <see cref="PgInstallationSource"/> instance.</param>
    /// <param name="filePath">The file path of the Zonky test binaries archive.</param>
    /// <returns>The updated <see cref="PgInstallationSource"/> instance.</returns>
    public static PgInstallationSource UseZonkyioMain(this PgInstallationSource source, string filePath)
        => source.UseMain(filePath, KnownExtractionStrategies.Zonky);

    /// <summary>
    /// Download a PostgreSQL extension artifact from the web.
    /// </summary>
    /// <param name="source">The <see cref="PgInstallationSource"/> instance.</param>
    /// <param name="downloadUrl">The URL to download the extension artifact from.</param>
    /// <param name="forceDownload">Specifies whether to force the download even if the artifact already exists.</param>
    /// <returns>The updated <see cref="PgInstallationSource"/> instance.</returns>
    public static PgInstallationSource UseWebExtension(this PgInstallationSource source, string downloadUrl, bool forceDownload = false)
        => source.UseExtension(
            new PgArtifact
            {
                Source = downloadUrl,
                IsLocal = false,
            }
        );

    /// <summary>
    /// Use a local binaries archive as the main PostgreSQL extension.
    /// </summary>
    /// <param name="source">The <see cref="PgInstallationSource"/> instance.</param>
    /// <param name="filePath">The file path of the local binaries archive.</param>
    /// <returns>The updated <see cref="PgInstallationSource"/> instance.</returns>
    public static PgInstallationSource UseLocalExtension(this PgInstallationSource source, string filePath)
        => source.UseExtension(
            new PgArtifact
            {
                Source = filePath,
                IsLocal = true,
            }
        );
}
