using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres;

/// <summary>
/// Provides methods to retrieve PostgreSQL standard binaries for different platforms and versions.
/// </summary>
public class PgStandardBinaries
{
    private const string PgVersionLatest = "17.0.0";

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
        // TODO : Fix/Update this mess
        return platform switch
        {
            { Platform: PgPlatform.PlatformWindows, Architecture: PgPlatform.ArchAmd64 } => pgVersion switch
            {
                "17.0.0" => new PgArtifact
                {
                    Kind = PgArtifactKind.Main,
                    Source = "https://sbp.enterprisedb.com/getfile.jsp?fileid=1259175",
                    Force = forceDownload
                },
                "16.0.0" => new PgArtifact
                {
                    Kind = PgArtifactKind.Main,
                    Source = "https://sbp.enterprisedb.com/getfile.jsp?fileid=1259178",
                    Force = forceDownload
                },
                _ => throw new NotSupportedException("Unsupported PostgreSQL version for Windows amd64")
            },

            { Platform: PgPlatform.PlatformDarwin, Architecture: PgPlatform.ArchAmd64 } => pgVersion switch
            {
                "17.0.0" => new PgArtifact
                {
                    Kind = PgArtifactKind.Main,
                    Source = "https://sbp.enterprisedb.com/getfile.jsp?fileid=1259171",
                    Force = forceDownload
                },
                "16.0.0" => new PgArtifact
                {
                    Kind = PgArtifactKind.Main,
                    Source = "https://sbp.enterprisedb.com/getfile.jsp?fileid=1259130",
                    Force = forceDownload
                },
                _ => throw new NotSupportedException("Unsupported PostgreSQL version for Darwin amd64")
            },

            { Platform: PgPlatform.PlatformLinux, Architecture: PgPlatform.ArchAmd64 } => pgVersion switch
            {
                _ => throw new NotSupportedException("Unsupported PostgreSQL version for Linux amd64")
            },
            _ => throw new NotSupportedException("Unsupported platform architecture")
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
}
