using EmbeddedPostgres.Constants;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres;

/// <summary>
/// Handy structure to build installations of PostgreSQL binaries from the Zonky repository.
/// </summary>
public class PgIoZonkyTestBinaries
{
    private const string DefaultMavenRepository = "https://repo1.maven.org/maven2";
    private const string PgVersionLatest = "17.0.0";

    /// <summary>
    /// Gets the latest PostgreSQL artifact for the current platform from the Zonky repository.
    /// </summary>
    /// <param name="forceDownload">If set to <c>true</c>, forces a download even if the artifact already exists.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the latest PostgreSQL binary.</returns>
    static public PgArtifact Latest(bool forceDownload = false)
        => WebArtifact(PgVersionLatest, PgPlatform.Current, forceDownload: forceDownload);

    /// <summary>
    /// Retrieves the PostgreSQL artifact from the Zonky repository based on the specified version and platform.
    /// </summary>
    /// <param name="pgVersion">The version of PostgreSQL to retrieve.</param>
    /// <param name="platform">The platform for which to retrieve the artifact.</param>
    /// <param name="mavenRepo">The Maven repository URL (default is <c>https://repo1.maven.org/maven2</c>).</param>
    /// <param name="forceDownload">If set to <c>true</c>, forces a download even if the artifact already exists.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the specified PostgreSQL binary.</returns>
    static public PgArtifact WebArtifact(string pgVersion, PgPlatform platform, string mavenRepo = DefaultMavenRepository, bool forceDownload = false)
    {
        var variant = platform.Architecture;
        if (!string.IsNullOrEmpty(platform.Distribution))
        {
            variant += $"-{platform.Distribution}";
        }

        return new PgArtifact
        {
            Kind = PgArtifactKind.Main,
            IsLocal = false,
            Source = $"{mavenRepo}/io/zonky/test/postgres/embedded-postgres-binaries-{platform.Platform}-{variant}/" +
                     $"{pgVersion}/embedded-postgres-binaries-{platform.Platform}-{variant}-{pgVersion}.jar",
            Force = forceDownload,
            ExtractionStrategy = KnownExtractionStrategies.Zonky
        };
    }

    /// <summary>
    /// Creates a local PostgreSQL artifact based on the specified file path.
    /// </summary>
    /// <param name="filePath">The file path of the local PostgreSQL binary.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the local PostgreSQL binary.</returns>
    static public PgArtifact LocalArtifact(string filePath)
    {
        return new PgArtifact
        {
            Kind = PgArtifactKind.Main,
            IsLocal = true,
            Source = filePath,
            ExtractionStrategy = KnownExtractionStrategies.Zonky
        };
    }
}
