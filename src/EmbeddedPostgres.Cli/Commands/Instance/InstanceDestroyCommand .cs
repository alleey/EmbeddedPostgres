using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

[Command("instance destroy")]
public class InstanceDestroyCommand : ICommand
{
    private readonly PgServerBuilder serverBuilder;

    public InstanceDestroyCommand(PgServerBuilder serverBuilder)
    {
        this.serverBuilder = serverBuilder;
    }

    [CommandOption("instance-directory", 'i', Description = "Directory for the PostgreSQL instance.")]
    public string InstanceDirectory { get; init; } = "postgres-test";

    [CommandOption("shutdown-mode", 'm', Description = "Specifies the shutdown mode (Smart, Fast, Immediate).")]
    public PgShutdownParams.ShutdownMode Mode { get; init; } = PgShutdownParams.ShutdownMode.Smart;

    [CommandOption("wait", 'w', Description = "Indicates whether to wait for the shutdown to complete.")]
    public bool Wait { get; init; } = true;

    [CommandOption("wait-timeout", 't', Description = "Timeout in seconds to wait for shutdown to complete.")]
    public int WaitTimeoutSecs { get; init; } = 180;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            PgServerBuilderOptions options = new()
            {
                InstanceDirectory = InstanceDirectory,
            };

            PgShutdownParams shutdownParams = new()
            {
                Mode = Mode,
                Wait = Wait,
                WaitTimeoutSecs = WaitTimeoutSecs
            };

            await serverBuilder.DestroyAsync(options, shutdownParams);
            await console.Output.WriteLineAsync("Instance destroyed successfully.");
        }
        catch (Exception ex)
        {
            await console.Error.WriteLineAsync(ex.Message);
        }

    }
}