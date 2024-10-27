namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the type of PostgreSQL artifact.
/// </summary>
public enum PgArtifactKind
{
    /// <summary>
    /// Indicates a main PostgreSQL binary artifact.
    /// </summary>
    Main,

    /// <summary>
    /// Indicates a PostgreSQL extension artifact.
    /// </summary>
    Extension
}

/// <summary>
/// Represents a PostgreSQL artifact with associated metadata.
/// </summary>
public record PgArtifact
{
    /// <summary>
    /// Gets the source location of the artifact, which can be a URL or file path.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the kind of artifact (e.g., <see cref="PgArtifactKind.Main"/> or <see cref="PgArtifactKind.Extension"/>).
    /// </summary>
    internal PgArtifactKind Kind { get; init; }

    /// <summary>
    /// Gets the target directory where the artifact should be stored or extracted.
    /// </summary>
    internal string Target { get; init; }

    /// <summary>
    /// Gets a value indicating whether the artifact is local (i.e., exists on the file system).
    /// Defaults to <c>false</c>.
    /// </summary>
    internal bool IsLocal { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether to force the download or extraction of the artifact, even if it already exists.
    /// Defaults to <c>false</c>.
    /// </summary>
    internal bool Force { get; init; } = false;

    /// <summary>
    /// Gets the strategy used for extracting the artifact.
    /// Defaults to an empty string.
    /// </summary>
    internal string ExtractionStrategy { get; init; } = string.Empty;
}
