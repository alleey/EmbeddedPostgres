namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Corresponds to the latest postmaster.pid schema
/// </summary>
/// <param name="Pid"></param>
/// <param name="DataDirectory"></param>
/// <param name="StartTime"></param>
/// <param name="Port"></param>
/// <param name="Host"></param>
public record PgRuntimeStatus
{
    public int StatusError { get; init; } = 0;
    public int Pid { get; init; } = 0;

    public string DataDirectory { get; init; }
    public long StartTime { get; init; }
    public int Port { get; init; }
    public string Host { get; init; }

    public bool IsValid => Pid != 0;

    public static PgRuntimeStatus Invalid = new();
}
