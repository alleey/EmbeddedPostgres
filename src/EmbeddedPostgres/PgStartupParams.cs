namespace EmbeddedPostgres;

/// <summary>
/// Represents the startup parameters for a PostgreSQL instance.
/// </summary>
public record PgStartupParams
{
    /// <summary>
    /// Gets or sets a value indicating whether to wait for the PostgreSQL instance to start.
    /// Default value is <c>false</c>.
    /// </summary>
    public bool Wait { get; init; } = false;

    /// <summary>
    /// Gets or sets the timeout in seconds to wait for the PostgreSQL instance to start.
    /// Default value is <c>30</c> seconds.
    /// </summary>
    public int WaitTimeoutSecs { get; init; } = 30;

    /// <summary>
    /// Gets the default instance of <see cref="PgStartupParams"/> with default values.
    /// </summary>
    public static PgStartupParams Default { get; } = new();
}
