namespace EmbeddedPostgres.Core.Interfaces;

public record PgRestoreArchiveOptions
{
    public bool ForceReInitialization { get; set; } = false;
    public string ArchiveFilePath { get; set; }
}
