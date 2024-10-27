namespace EmbeddedPostgres.Core.Interfaces;

public record PgInitDbOptions
{
    public bool ForceReInitialization { get; set; } = false;
    public static PgInitDbOptions Default { get; } = new();
}
