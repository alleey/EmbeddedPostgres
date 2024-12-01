using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.DependencyInjection;
using EmbeddedPostgres.Infrastructure.Interfaces;
using EmbeddedPostgres.Tests.Extensions;
using EmbeddedPostgres.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Integration.Tests;

[TestClass()]
public class StandardServerIntegrationTests
{
    private const string PgUser = "postgres";
    private PgServerBuilder pgServerBuilder;

    public StandardServerIntegrationTests()
    {
        var builder = new HostBuilder();

        // Configure services
        builder.ConfigureServices((hostContext, services) =>
        {
            services.AddEmbeddedPostgresCoreServices();
            services.AddEmbeddedPostgresServices();
        });

        var host = builder.Build();
        pgServerBuilder = host.Services.GetService<PgServerBuilder>();
    }

    [TestMethod()]
    public async Task CreateServerAndImportDump()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerAndImportDump";
                builder.ServerArtifact = PgStandardBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort();
                });
            })
        );

        var factory = PgClusterInitializerFactory.FromEnvironment(pgServer.Environment);

        // Default initialize the primary data cluster and create an Archive before starting it
        //
        await pgServer.InitializeAsync(
            ["primary"],
            initializer: (cluster) => factory.InitializeUsingInitDb()
        );
        await pgServer.StartAsync(["primary"], startupParams: PgStartupParams.Default with { Wait = true });

        try
        {
            await pgServer.Environment.DownloadExtractAsync(
                "https://github.com/gordonkwokkwok/DVD-Rental-PostgreSQL-Project/raw/refs/heads/main/dataset/dvdrental.tar",
                destDirectory: "downloads",
                cacheDirectory: "downloads",
                cacheFilename: "dvdrental-download.tar" // this tar has the same name as its container (dvdrental.tar inside dvdrental.tar),
                                                        // we want to download and extract to same folder, but then it must be renamed
            );

            var options = new PgRestoreDumpOptions();
            options.Source = Path.Combine("downloads", "dvdrental.tar");
            options.ConnectDatabaseName = "postgres";
            options.CreateTargetDatabase = true;
            options.DropTargetDatabase = true;

            await pgServer.ImportDumpAsync("primary", options);
            var records = await pgServer.ExecuteReaderAsync("primary", "SELECT * FROM staff;", database: "dvdrental").Take(25).ToListAsync();

        }
        finally
        {
            //await pgServer.StopAsync(["primary"], shutdownParams: PgShutdownParams.Fast);
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }


    [TestMethod()]
    public async Task CreateServerAndExportDump()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerAndExportDump";
                builder.ServerArtifact = PgStandardBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort();
                });
            })
        );

        var factory = PgClusterInitializerFactory.FromEnvironment(pgServer.Environment);

        // Default initialize the primary data cluster and create an Archive before starting it
        //
        await pgServer.InitializeAsync(
            ["primary"],
            initializer: (cluster) => factory.InitializeUsingInitDb()
        );
        await pgServer.StartAsync(["primary"], startupParams: PgStartupParams.Default with { Wait = true });

        try
        {
            // Create books table and insert some records
            await pgServer.TestConnectionAsync("primary");
            var options = new PgExportDumpOptions();
            options.DatabaseName = "postgres";
            options.Target = "archives.tar";
            options.TargetFormat = "t";

            await pgServer.ExportDumpAsync("primary", options);
        }
        finally
        {
            await pgServer.StopAsync(["primary"], shutdownParams: PgShutdownParams.Fast);
            //await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }
}
