using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.DependencyInjection;
using EmbeddedPostgres.Extensions;
using EmbeddedPostgres.Tests.Extensions;
using EmbeddedPostgres.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace EmbeddedPostgres.Integration.Tests;

[TestClass()]
public class MinimalServerIntegrationTests
{
    private const string PgUser = "IntegrationTestUser";
    private PgServerBuilder pgServerBuilder;

    public MinimalServerIntegrationTests()
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
    public async Task CreateServerAndExecuteSql()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateMinimalServerAndExecuteSql";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
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

        try
        {
            await pgServer.StartAsync(startupParams: PgStartupParams.Default with { Wait = true });
            await pgServer.TestConnectionAsync("primary");
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }

    [TestMethod()]
    public async Task CreateServerAndGISExtensionExecuteSql()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerAndGISExtensionExecuteSql";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;
                builder.AddPostgresExtension("https://download.osgeo.org/postgis/windows/pg17/postgis-bundle-pg17-3.5.0x64.zip");
                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort();
                });
            })
        );

        try
        {
            await pgServer.StartAsync(
                startupParams: PgStartupParams.Default with { Wait = true },
                eventListener: async (evt, cancellationToken) =>
                {
                    if (evt.IsSuccess)
                    {
                        await pgServer.TestConnectionAsync(evt.DataCluster.UniqueId);
                    }
                }
            );
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }

    [TestMethod()]
    public async Task CreateServerWithAdditionalServerParameters()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerWithAdditionalServerParameters";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;
                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort();
                    cluster.AddClusterParameters(new Dictionary<string, string> {
                        
                        // set generic query optimizer to off
                        { "geqo", "off" },

                        // set timezone as UTC
                        { "timezone", "UTC" },

                        // switch off synchronous commit
                        { "synchronous_commit", "off" },

                        // set max connections
                        { "max_connections", "4" },
                    });
                });
            })
        );

        try
        {
            await pgServer.StartAsync(
                startupParams: PgStartupParams.Default with { Wait = true },
                eventListener: async (evt, cancellationToken) =>
                {
                    if (evt.IsSuccess)
                    {
                        var connStr = string.Format(TestPgServerExtensions.ConnectionStringTemplate, evt.DataCluster.Settings.Port, evt.DataCluster.Settings.Superuser);
                        using var conn = new Npgsql.NpgsqlConnection(connStr);
                        var cmd = new Npgsql.NpgsqlCommand(TestPgServerExtensions.DefaultTestSQL, conn);

                        // Open first connection
                        await conn.OpenAsync();

                        var exception = await Assert.ThrowsExceptionAsync<Npgsql.PostgresException>(async () =>
                        {
                            // Try to open 4 additional connections
                            Npgsql.NpgsqlConnection[] connections = [
                                new (connStr), new (connStr), new (connStr), new (connStr)
                            ];
                            foreach (var connection in connections)
                            {
                                await connection.OpenAsync();
                            }
                            foreach (var connection in connections)
                            {
                                await conn.CloseAsync();
                            }
                        });

                        Assert.IsTrue(exception.Message.Contains("sorry, too many clients already"));
                    }
                }
            );
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }

    [TestMethod()]
    public async Task ReloadConfirugartionWorks()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "ReloadConfirugartionWorks";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
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

        try
        {
            await pgServer.StartAsync(
                startupParams: PgStartupParams.Default with { Wait = true },
                eventListener: async (evt, cancellationToken) =>
                {
                    if (evt.IsSuccess)
                    {
                        var databaseDirectory = evt.DataCluster.GetDataFullPath();
                        var confFile = Path.Combine(databaseDirectory, "postgresql.conf");
                        var externalPid = Path.Combine(Path.GetDirectoryName(confFile), "ReloadConfirugartionWorks.pid");

                        File.AppendAllText(confFile, $"external_pid_file = 'ReloadConfirugartionWorks.pid'\n");

                        await pgServer.ReloadConfigurationAsync();
                        await Task.Delay(1000);

                        Assert.IsTrue(File.Exists(externalPid), $"External pid file must exist {externalPid}");
                    }
                }
            );
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }

    [TestMethod()]
    public async Task CreateServerWithMultipleClustters()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerWithMultipleClustters";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort(5500);
                });

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "standby1";
                    cluster.DataDirectory = "data1";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort(5600);
                });

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "standby2";
                    cluster.DataDirectory = "data2";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort(5700);
                });
            })
        );

        try
        {
            await pgServer.StartAsync(startupParams: PgStartupParams.Default with { Wait = true });

            await pgServer.TestConnectionAsync("primary");
            await pgServer.TestConnectionAsync("standby1");
            await pgServer.TestConnectionAsync("standby2");
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }

    [TestMethod()]
    public async Task CreateStandbyFromPrimaryArchive()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateStandbyFromPrimaryArchive";
                builder.ServerArtifact = PgIoZonkyTestBinaries.Latest(forceDownload: false);
                builder.CleanInstall = true;

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "primary";
                    cluster.DataDirectory = "data";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort(5500);
                });

                builder.AddDataCluster(cluster =>
                {
                    cluster.UniqueId = "standby1";
                    cluster.DataDirectory = "data1";
                    cluster.Superuser = PgUser;
                    cluster.Port = Helpers.GetAvailablePort(5600);
                });

            })
        );

        var factory = PgClusterInitializerFactory.FromEnvironment(pgServer.Environment);
        var archiveFileFresh = Path.Combine(pgServer.GetInstanceFullPath(), "primary-fresh.zip");
        var archiveFile = Path.Combine(pgServer.GetInstanceFullPath(), "primary.zip");

        try
        {
            // Default initialize the primary data cluster and create an Archive before starting it
            //
            await pgServer.InitializeAsync(
                ["primary"],
                initializer: (cluster) => factory.InitializeUsingInitDb(),
                eventListener: async (evt, cancellationToken) =>
                {
                    if (evt.IsSuccess)
                    {
                        // Take archive of freshly initialized data cluster
                        await evt.DataCluster.ArchiveAsync(archiveFileFresh);
                    }
                }
            );
            await pgServer.StartAsync(["primary"], startupParams: PgStartupParams.Default with { Wait = true });

            // CreateTargetDatabase a table and insert some data
            await pgServer.TestConnectionAsync("primary");

            await pgServer.StopAsync(["primary"]);
            await pgServer.ArchiveAsync("primary", archiveFile);

            // Use the primary's archive to initialize the standby cluster, which should have the books table and data
            await pgServer.InitializeAsync(
                ["standby1"],
                initializer: (cluster) => factory.RestoreFromArchive(options =>
                {
                    options.ArchiveFilePath = archiveFile;
                })
            );
            await pgServer.StartAsync(["standby1"], startupParams: PgStartupParams.Default with { Wait = true });
            var records = await pgServer.ExecuteReaderAsync("standby1", "SELECT * FROM books;")
                .Take(10)
                .ToListAsync();
        }
        finally
        {
            await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
        }
    }
}
