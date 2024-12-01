using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace EmbeddedPostgres.Cli.Commands.Instance;

[Command("instance create")]
public class InstanceCreateCommand : ICommand
{
    private readonly PgServerBuilder serverBuilder;

    public InstanceCreateCommand(PgServerBuilder serverBuilder)
    {
        this.serverBuilder = serverBuilder;
    }

    [CommandOption("cache-directory", 'c', Description = "Directory to cache downloads.")]
    public string CacheDirectory { get; init; } = "downloads";

    [CommandOption("instance-directory", 'i', Description = "Directory for the PostgreSQL instance.")]
    public string InstanceDirectory { get; init; } = "postgres-test";

    [CommandOption("server-artifact", 's', Description = "Specifies the PostgreSQL server binaries.")]
    public string ServerArtifact { get; init; } = PgStandardBinaries.Latest().Source;

    [CommandOption("clean-install", 'C', Description = "Indicates whether to perform a clean installation.")]
    public bool CleanInstall { get; init; } = true;

    [CommandOption("exclude-pgadmin", 'e', Description = "Exclude pgAdmin from installation.")]
    public bool ExcludePgAdminInstallation { get; init; } = true;

    [CommandOption("extension", 'x', Description = "URLs/Paths of PostgreSQL extensions to install.")]
    public List<string> Extensions { get; init; } = new();

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            var env = await serverBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = CacheDirectory;
                builder.InstanceDirectory = InstanceDirectory;
                builder.ServerArtifact = PgCustomBinaries.Artifact(ServerArtifact);
                builder.CleanInstall = CleanInstall;
                builder.ExcludePgAdminInstallation = ExcludePgAdminInstallation;

                // Add each specified extensions
                foreach (var extensionUrl in Extensions)
                {
                    builder.AddPostgresExtension(extensionUrl);
                }
            });

            await console.Output.WriteLineAsync("Instance created successfully.");
        }
        catch (Exception ex)
        {
            await console.Error.WriteLineAsync(ex.Message);
        }
    }
}