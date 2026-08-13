using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres;

/// <summary>
/// Provides methods to retrieve PostgreSQL standard binaries for different platforms and versions.
/// </summary>
public class PgStandardBinaries
{
    private const string PgVersionLatest = "18.0.0";

    /// <summary>
    /// EnterpriseDB serves its binaries under a readable, versioned path, for example
    /// <c>https://get.enterprisedb.com/postgresql/postgresql-17.10-2-windows-x64-binaries.zip</c>.
    /// The two placeholders are the EnterpriseDB build and the platform suffix.
    /// </summary>
    private const string DownloadUrlFormat = "https://get.enterprisedb.com/postgresql/postgresql-{0}-{1}-binaries.zip";

    /// <summary>
    /// Maps a supported PostgreSQL version to the EnterpriseDB build that serves it.
    /// Bump the build here to pick up a newer patch release.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> EdbBuilds = new Dictionary<string, string>
    {
        ["18.0.0"] = "18.3-1",
        ["17.0.0"] = "17.10-2",
        ["16.0.0"] = "16.14-1",
    };

    /// <summary>The major versions this class can resolve, newest first.</summary>
    public static IReadOnlyCollection<string> SupportedVersions => EdbBuilds.Keys.ToArray();

    /// <summary>
    /// Gets the latest PostgreSQL artifact for the current platform.
    /// </summary>
    /// <param name="forceDownload">If set to <c>true</c>, forces a download even if the artifact already exists.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the latest PostgreSQL binary.</returns>
    static public PgArtifact Latest(bool forceDownload = false)
        => WebArtifact(PgVersionLatest, PgPlatform.Current, forceDownload: forceDownload);

    /// <summary>
    /// Retrieves the PostgreSQL artifact from the web based on the specified version and platform.
    /// </summary>
    /// <param name="pgVersion">The version of PostgreSQL to retrieve.</param>
    /// <param name="platform">The platform for which to retrieve the artifact.</param>
    /// <param name="forceDownload">If set to <c>true</c>, forces a download even if the artifact already exists.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the specified PostgreSQL binary.</returns>
    /// <exception cref="NotSupportedException">Thrown if the specified PostgreSQL version or platform is unsupported.</exception>
    static public PgArtifact WebArtifact(string pgVersion, PgPlatform platform, bool forceDownload = false)
    {
        var target = ResolvePlatform(platform);
        var build = ResolveBuild(pgVersion);

        return new PgArtifact
        {
            Kind = PgArtifactKind.Main,
            Source = string.Format(DownloadUrlFormat, build, target),
            Force = forceDownload
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
            Source = filePath
        };
    }

    /// <summary>
    /// Resolves the EnterpriseDB archive suffix for the given platform. EnterpriseDB does not
    /// publish Linux binaries in this form, so Linux is deliberately unsupported here.
    /// </summary>
    private static string ResolvePlatform(PgPlatform platform)
        => platform switch
        {
            { Platform: PgPlatform.PlatformWindows, Architecture: PgPlatform.ArchAmd64 } => "windows-x64",
            { Platform: PgPlatform.PlatformDarwin, Architecture: PgPlatform.ArchAmd64 } => "osx",
            _ => throw new NotSupportedException($"Unsupported platform architecture: {platform.Platform}/{platform.Architecture}")
        };

    /// <summary>
    /// Resolves the pinned EnterpriseDB build for the given PostgreSQL version.
    /// </summary>
    /// <remarks>
    /// Callers say "18", "18.0" or "18.0.0" and mean the same major release, so the
    /// lookup is keyed on the major version only. A specific patch build is chosen
    /// by the table above, or bypassed entirely with a custom artifact URL.
    /// </remarks>
    private static string ResolveBuild(string pgVersion)
    {
        var major = (pgVersion ?? string.Empty).Split('.')[0];
        if (EdbBuilds.TryGetValue($"{major}.0.0", out var build))
        {
            return build;
        }

        throw new NotSupportedException(
            $"Unsupported PostgreSQL version: {pgVersion}. Supported: "
            + string.Join(", ", EdbBuilds.Keys) + ". Use a custom artifact URL for anything else.");
    }
}
