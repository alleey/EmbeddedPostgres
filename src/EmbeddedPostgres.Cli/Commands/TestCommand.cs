using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Cli.Commands;

[Command("test")]
public class TestCommand : ICommand
{
    private readonly PgServerBuilder serverBuilder;
    private readonly IFileCompressor compressor;

    public TestCommand(PgServerBuilder serverBuilder, IFileCompressor compressor)
    {
        this.serverBuilder = serverBuilder;
        this.compressor = compressor;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        PgServer pgServer = new PgServer(
            await serverBuilder.BuildAsync(builder => 
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "postgres-test";
                builder.ServerArtifact = PgStandardBinaries.Latest();
                //builder.ServerArtifact = PgZonkyioBinaries.Latest();
                builder.CleanInstall = true;
                builder.ExcludePgAdminInstallation = true;

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Port = Helpers.GetAvailablePort();
                });

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "standby1";
                    cluster.DataDirectory = "data1";
                    cluster.Port = Helpers.GetAvailablePort();
                });

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "standby2";
                    cluster.DataDirectory = "data2";
                    cluster.Port = Helpers.GetAvailablePort();
                });
            })
        );

        console.Output.WriteLine($"Initializing data cluster ...");

        console.Output.WriteLine($"Starting server ...");
        await pgServer.StartAsync(PgServer.AllDataClusters, PgStartupParams.Default with { Wait = true });

        await pgServer.ListDatabasesAsync(
            "primary", 
            (db, ct) => {
                console.Output.WriteLine(db);
                return Task.CompletedTask;
            });

        var cluster = pgServer.GetClusterByUniqueId("primary");
        console.Output.WriteLine($"ClusterControllers listening on {cluster.Settings.Port}, press ENTER to terminate ...");
        console.Input.ReadLine();

        console.Output.WriteLine($"Terminating instance ...");
        await serverBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }
}
