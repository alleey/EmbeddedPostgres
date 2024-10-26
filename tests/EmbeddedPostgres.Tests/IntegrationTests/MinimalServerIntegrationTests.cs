using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.DependencyInjection;
using EmbeddedPostgres.Extensions;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using EmbeddedPostgres.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Integration.Tests;

[TestClass()]
public class MinimalServerIntegrationTests
{
    private const string PgUser = "IntegrationTestUser";
    private const string ConnStr = "Host=localhost;Port={0};User Id={1};Password=test;Database=postgres;Pooling=false";
    private readonly IFileSystem fileSystem;
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
        fileSystem = host.Services.GetService<IFileSystem>(); ;
    }

    static readonly string DefaultTestSQL = """"

        -- 1. Create a 'books' table
        CREATE TABLE books (
            id SERIAL PRIMARY KEY,
            title VARCHAR(255) NOT NULL,
            author VARCHAR(255) NOT NULL,
            published_year INT,
            genre VARCHAR(100),
            price DECIMAL(10, 2),
            stock_quantity INT DEFAULT 0,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );

        -- 2. Insert multiple rows into the 'books' table
        INSERT INTO books (title, author, published_year, genre, price, stock_quantity) VALUES
        ('To Kill a Mockingbird', 'Harper Lee', 1960, 'Fiction', 10.99, 5),
        ('1984', 'George Orwell', 1949, 'Dystopian', 8.99, 10),
        ('Pride and Prejudice', 'Jane Austen', 1813, 'Romance', 12.50, 7),
        ('The Great Gatsby', 'F. Scott Fitzgerald', 1925, 'Classic', 9.99, 3),
        ('Moby Dick', 'Herman Melville', 1851, 'Adventure', 15.20, 2),
        ('War and Peace', 'Leo Tolstoy', 1869, 'Historical Fiction', 20.00, 4),
        ('The Catcher in the Rye', 'J.D. Salinger', 1951, 'Fiction', 6.99, 12),
        ('The Hobbit', 'J.R.R. Tolkien', 1937, 'Fantasy', 18.75, 9);

        -- 3. Retrieve all rows from the 'books' table
        SELECT * FROM books;

        """";


    [TestMethod()]
    public async Task CreateServerAndExecuteSql()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateMinimalServerAndExecuteSql";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        await pgServer.StartAsync(startupParams: PgStartupParams.Default with { Wait = true });

        var cluster = pgServer.GetClusterByUniqueId("primary");
        var connStr = string.Format(ConnStr, cluster.Settings.Port, cluster.Settings.Superuser);
        using var conn = new Npgsql.NpgsqlConnection(connStr);
        var cmd = new Npgsql.NpgsqlCommand(DefaultTestSQL, conn);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
        await conn.CloseAsync();

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    [TestMethod()]
    public async Task CreateServerAndGISExtensionExecuteSql()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerAndGISExtensionExecuteSql";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        await pgServer.StartAsync(
            startupParams: PgStartupParams.Default with { Wait = true },
            eventListener: async (evt, cancellationToken) =>
            {
                if (evt.IsSuccess)
                {
                    var connStr = string.Format(ConnStr, evt.DataCluster.Settings.Port, evt.DataCluster.Settings.Superuser);
                    using var conn = new Npgsql.NpgsqlConnection(connStr);
                    var cmd = new Npgsql.NpgsqlCommand(DefaultTestSQL, conn);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                    await conn.CloseAsync();
                }
            }
        );

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    [TestMethod()]
    public async Task CreateServerWithAdditionalServerParameters()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerWithAdditionalServerParameters";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        await pgServer.StartAsync(
            startupParams: PgStartupParams.Default with { Wait = true },
            eventListener: async (evt, cancellationToken) =>
            {
                if (evt.IsSuccess)
                {
                    var connStr = string.Format(ConnStr, evt.DataCluster.Settings.Port, evt.DataCluster.Settings.Superuser);
                    using var conn = new Npgsql.NpgsqlConnection(connStr);
                    var cmd = new Npgsql.NpgsqlCommand(DefaultTestSQL, conn);

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

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    [TestMethod()]
    public async Task ReloadConfirugartionWorks()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "ReloadConfirugartionWorks";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    [TestMethod()]
    public async Task CreateServerWithMultipleClustters()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateServerWithMultipleClustters";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        await pgServer.StartAsync(startupParams: PgStartupParams.Default with { Wait = true });

        await TestConnection(pgServer, "primary");
        await TestConnection(pgServer, "standby1");
        await TestConnection(pgServer, "standby2");

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    [TestMethod()]
    public async Task CreateStandbyFromPrimaryArchive()
    {
        PgServer pgServer = new PgServer(
            await pgServerBuilder.BuildAsync(builder =>
            {
                builder.CacheDirectory = "downloads";
                builder.InstanceDirectory = "CreateStandbyFromPrimaryArchive";
                builder.ServerArtifact = PgZonkyioBinaries.Latest(forceDownload: false);
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

        var factory = PgInitializationSourceFactory.FromEnvironment(pgServer.Environment);
        var archiveFileFresh = Path.Combine(pgServer.Environment.Instance.GetInstanceFullPath(), "primary-fresh.zip");
        var archiveFile = Path.Combine(pgServer.Environment.Instance.GetInstanceFullPath(), "primary.zip");

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
        
        // Create a table and insert some data
        await TestConnection(pgServer, "primary");

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
        var records = await TestConnection(pgServer, "standby1", "SELECT * FROM books;");

        await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
    }

    static async Task<List<Dictionary<string, object>>> TestConnection(PgServer server, string clusterId, string sql = null)
    {
        var cluster = server.GetClusterByUniqueId(clusterId);
        var connStr = string.Format(ConnStr, cluster.Settings.Port, cluster.Settings.Superuser);
        var conn = new Npgsql.NpgsqlConnection(connStr);
        var cmd = new Npgsql.NpgsqlCommand(sql ?? DefaultTestSQL, conn);

        await conn.OpenAsync();
        var reader = await cmd.ExecuteReaderAsync();
        var results = new List<Dictionary<string, object>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.GetValue(i);
                row[columnName] = value;
            }

            results.Add(row);
        }

        await conn.CloseAsync();
        return results;
    }
}