namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Represents the parameters for shutting down a PostgreSQL instance.
/// </summary>
public record PgShutdownParams
{
    /// <summary>
    /// Specifies the shutdown mode for the PostgreSQL instance.
    /// </summary>
    public enum ShutdownMode
    {
        /// <summary>
        /// Allows active connections to complete before shutting down.
        /// </summary>
        Smart,

        /// <summary>
        /// Terminates all connections immediately.
        /// </summary>
        Immediate,

        /// <summary>
        /// Stops the server as quickly as possible without waiting for active connections.
        /// </summary>
        Fast
    }

    /// <summary>
    /// Gets or sets the shutdown mode. Defaults to <see cref="ShutdownMode.Smart"/>.
    /// </summary>
    public ShutdownMode Mode { get; init; } = ShutdownMode.Smart;

    /// <summary>
    /// Gets or sets a value indicating whether to wait for the shutdown to complete.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Wait { get; init; } = true;

    /// <summary>
    /// Gets or sets the timeout duration in seconds to wait for the shutdown to complete.
    /// Defaults to 180 seconds.
    /// </summary>
    public int WaitTimeoutSecs { get; init; } = 180;

    /// <summary>
    /// Gets the default shutdown parameters with <see cref="ShutdownMode.Smart"/> mode.
    /// </summary>
    public static PgShutdownParams Default { get; } = new();

    /// <summary>
    /// Gets the shutdown parameters for an immediate shutdown.
    /// </summary>
    public static PgShutdownParams Immediate { get; } = Default with { Mode = ShutdownMode.Immediate };

    /// <summary>
    /// Gets the shutdown parameters for a fast shutdown.
    /// </summary>
    public static PgShutdownParams Fast { get; } = Default with { Mode = ShutdownMode.Fast };

    /// <summary>
    /// Gets the shutdown parameters for a smart shutdown (default).
    /// </summary>
    public static PgShutdownParams Smart { get; } = Default;
}
