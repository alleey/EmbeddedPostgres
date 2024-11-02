# EmbeddedPostgres

One of the forks of https://github.com/mysticmind/mysticmind-postgresembed. The library is divided into a core framework and a convenient abstraction based on the core lib. Both libraries are fully async.

## Usage
Install the package from Nuget using `Install-Package EmbeddedPostgres`, `Install-Package EmbeddedPostgres.Core` or clone the repository and build it.

The easiest way to start using the library is to setup dependency injection. This can be accomplished as:

```csharp
builder.ConfigureServices((hostContext, services) =>
{
    services.AddEmbeddedPostgresCoreServices();
    services.AddEmbeddedPostgresServices();
});
```

### Example of using Postgres minimal binaries from Zonkyiotest

```csharp
PgServerBuilder pgServerBuilder = host.Services.GetService<PgServerBuilder>();
PgServer pgServer = new PgServer(
    await pgServerBuilder.BuildAsync(builder =>
    {
        builder.CacheDirectory = "downloads";
        builder.InstanceDirectory = "primary";
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
```


### Example of passing additional server parameters
```csharp
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
...
```


### Example of creating multiple data clusters. Using archive of one cluster to initialize another cluster
```csharp
PgServerBuilder pgServerBuilder = host.Services.GetService<PgServerBuilder>();
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

var factory = PgClusterInitializerFactory.FromEnvironment(pgServer.Environment);
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
```

### Example of creating a data cluster and restoring database from a existing dump.
```
PgServer pgServer = new PgServer(
    await pgServerBuilder.BuildAsync(builder =>
    {
        builder.CacheDirectory = "downloads";
        builder.InstanceDirectory = "CreateServerAndImportDump";
        builder.ServerArtifact = PgStandardBinaries.Latest(forceDownload: false);
        builder.CleanInstall = false;

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
        cacheFilename: "dvdrental-download.tar"
    );

    var options = new PgRestoreDumpOptions();
    options.SourceFilename = Path.Combine("downloads", "dvdrental.tar");
    options.ConnectDatabaseName = "postgres";
    options.CreateTargetDatabase = true;
    options.DropTargetDatabase = true;

    await pgServer.ImportDumpAsync("primary", options);
    ...

}
finally
{
    await pgServer.StopAsync(["primary"], shutdownParams: PgShutdownParams.Fast);
    //await pgServerBuilder.DestroyAsync(pgServer, PgShutdownParams.Fast);
}
```
