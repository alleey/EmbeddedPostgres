using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres;

public class PgCustomBinaries
{
    /// <summary>
    /// Creates a local PostgreSQL artifact based on the specified file path.
    /// </summary>
    /// <param name="filePath">The file path of the local PostgreSQL binary.</param>
    /// <returns>A <see cref="PgArtifact"/> representing the local PostgreSQL binary.</returns>
    static public PgArtifact Artifact(string filePathOrUrl, string extractionStrategy = null)
    {
        return new PgArtifact
        {
            Kind = PgArtifactKind.Main,
            IsLocal = PathChecker.IsLocalPath(filePathOrUrl),
            Source = filePathOrUrl,
            ExtractionStrategy = extractionStrategy,
        };
    }
}
