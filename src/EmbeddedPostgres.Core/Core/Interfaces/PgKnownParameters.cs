namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Provides a collection of known parameter names used within the PostgreSQL context.
/// </summary>
public static class PgKnownParameters
{
    /// <summary>
    /// The parameter name for normalizing file attributes.
    /// </summary>
    public const string NormallizeAttributes = "NormallizeAttributes";

    /// <summary>
    /// Contains known parameters specific to Windows environments.
    /// </summary>
    public static class Windows
    {
        /// <summary>
        /// The parameter name for adding local user access permissions.
        /// </summary>
        public const string AddLocalUserAccessPermission = "AddLocalUserAccessPermission";
    }

    /// <summary>
    /// Contains known parameters specific to Linux environments.
    /// </summary>
    public static class Linux
    {
        /// <summary>
        /// The parameter name for setting executable attributes on files.
        /// </summary>
        public const string SetExecutableAttributes = "SetExecutableAttributes";
    }
}
