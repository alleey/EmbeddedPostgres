namespace EmbeddedPostgres.Constants;

/// <summary>
/// Provides a collection of known extraction strategy names used within the application.
/// </summary>
public class KnownExtractionStrategies
{
    /// <summary>
    /// The default extraction strategy name.
    /// </summary>
    public const string Default = Sharp;

    /// <summary>
    /// The extraction strategy name for the Sharp extraction method.
    /// </summary>
    public const string Sharp = "sharp";

    /// <summary>
    /// The extraction strategy name for the System extraction method.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// The extraction strategy name for the Zonky extraction method.
    /// </summary>
    public const string Zonky = "zonkyio";
}
