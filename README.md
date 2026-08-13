# EmbeddedPostgres

One of the forks of https://github.com/mysticmind/mysticmind-postgresembed. The library is divided into a core framework and a convenient abstraction based on the core lib. Both libraries are fully async.

## Command line

`empg` manages PostgreSQL instances from the shell, following git's conventions: instances are
named and registered the way `git remote` names repositories, and commands act on the active one —
or on the instance you happen to be standing in.

### Installing

`empg` is published as a .NET tool and needs the .NET 10 runtime:

```bash
dotnet tool install --global empg
```

Add `--version <version>` to pin a release, `dotnet tool update --global empg` to upgrade, or
`dotnet tool uninstall --global empg` to remove it.

### Getting started

```bash
empg instance create --port 55432    # install binaries and initdb a cluster
empg start                           # no path needed
empg sql "SELECT version()"
empg status
empg stop
```

### Instances

An instance is a PostgreSQL installation plus the clusters empg runs from it. Instances are named
and registered, the way `git remote` names repositories, so you refer to them by name rather than
by path:

```bash
empg instance list
empg instance use projectbase          # make it the default
empg -i projectbase status             # or address one explicitly
```

Every instance is one of two kinds, and the kind is the command that made it:

| | |
| --- | --- |
| `empg instance create [<path>]` | **managed** — empg installs the binaries and may delete them |
| `empg instance adopt <path>` | **adopted** — the binaries were already there and belong to someone else |

That distinction is what governs destructive behaviour: `instance destroy --purge` deletes the
installation for a managed instance and is refused for an adopted one. `empg instance show`
reports which kind you are looking at.

A third command covers an instance that already exists but is not registered on this machine —
one created earlier, or on a shared volume:

```bash
empg instance add <name> <path>        # register only; installs nothing
```

`create` and `adopt` register what they make, taking the directory name unless you pass `--name`
(`--no-register` opts out). The first instance registered becomes the active one.

An instance is chosen from the most explicit signal available:

| Order | Signal |
| --- | --- |
| 1 | `--instance <name>` (`-i`) — a registered name |
| 2 | `-C <path>` — a directory, registered or not |
| 3 | `EMPG_DIR` environment variable |
| 4 | walking up from the working directory |
| 5 | the active instance |

Standing inside an instance beats the active instance deliberately: being in a directory is a
clearer statement of intent than a default set at some earlier point. `empg instance show` prints
which instance a command would act on and which of these rules chose it.

As with git, `-i` and `-C` may be written before the command — `empg -i projectbase status`.

The registry holds only names and paths, in `EMPG_HOME` or your user application-data directory.
Everything describing an instance lives in that instance's own manifest, so losing the registry
costs you the names, not the data. `empg instance remove` forgets a name and deletes nothing.

### PostgreSQL versions

`instance create` installs the newest supported release by default. Pick another with the major
version — `--pg-version 17` and `--pg-version 17.0.0` mean the same thing:

```bash
empg instance create ./lab                      # newest supported (18)
empg instance create ./lab --pg-version 17      # a specific major
```

| Major | EnterpriseDB build |
| --- | --- |
| 18 | 18.3-1 (default) |
| 17 | 17.10-2 |
| 16 | 16.14-1 |

These are the standard EnterpriseDB distributions, Windows x64 and macOS; EnterpriseDB does not
publish Linux binaries in this form. For anything outside the table — a newer patch build, a Linux
tarball, a private mirror — pass the archive yourself with `-a/--artifact <url|path>`, which takes
precedence over `--pg-version`. `-m/--minimal` selects the Zonky test binaries instead and is
likewise mutually exclusive with `--pg-version`.

Every command accepts the same global options:

| Option | Description |
| --- | --- |
| `-i`, `--instance <name>` | Registered instance to act on; defaults to the active one |
| `-C`, `--directory <path>` | Act on the instance in this directory, registered or not |
| `--json` | Emit machine-readable JSON instead of text |
| `-q`, `--quiet` | Suppress informational output |
| `--connect-timeout <secs>` | Give up connecting to a cluster after this long (default 10; 0 waits indefinitely) |

### Commands

| Command | Description |
| --- | --- |
| `empg instance create [<path>]` | Create a managed instance: install binaries and initialise a cluster |
| `empg instance adopt <path>` | Adopt a PostgreSQL installation already on disk |
| `empg instance add <name> <path>` | Register an instance that already exists |
| `empg instance list` / `use <name>` / `show` / `remove <name>` | Manage named instances |
| `empg instance check` | Verify the instance's binaries |
| `empg instance destroy [<name>]` | Stop all clusters and delete their data; `--purge` also deletes the installation |
| `empg status` | Show the instance and the state of its clusters |
| `empg start` / `stop` / `restart` | Control the clusters |
| `empg reload` | Re-read configuration without restarting |
| `empg cluster add <name>` / `list` / `remove <name>` | Manage data clusters, like `git worktree` |
| `empg config set <key> <value>` / `get <key>` / `unset <key>` / `list` | Manage `postgresql.conf` parameters, like `git config` |
| `empg sql [<statement>]` | Execute SQL inline, as `-s/--sql`, or from a file with `-f` |
| `empg db list` | List databases in a cluster |
| `empg dump <target>` / `restore <source>` | Export and import with `pg_dump` / `pg_restore` |
| `empg archive <target>` | Stop a cluster and archive its data directory |
| `empg extension add <source>` / `list` | Install and list extensions |
| `empg listen <addresses>` | Set the interfaces a cluster binds to |
| `empg hba list` / `allow <cidr>` / `revoke <cidr>` / `harden` | Manage client authentication |
| `empg role add <name>` / `password <name>` / `list` | Manage database roles |
| `empg ident add <principal> <role>` / `list` | Map OS principals to roles for SSPI |
| `empg uri` | Print a connection URI |

### Command options

Beyond the three global options above:

| Command | Options |
| --- | --- |
| `instance create` / `adopt` | `-n/--name`, `--no-register`, `-c/--cluster`, `-p/--port` (`0` = auto), `--port-start` (5500), `-d/--data-directory`, `-u/--superuser`, `-l/--listen`, `--durable`, `--encoding`, `--locale`, `--bare` |
| `instance create` only | `--pg-version <major>` (18, 17, 16), `-a/--artifact <url\|path>`, `-m/--minimal` (Zonky binaries), `-f/--force` |
| `instance add` | `--use` to also make it active |
| `instance destroy` | `--purge`, `-m/--mode` (Fast), `-f/--force` |
| `start` | `-c/--cluster`, `-w/--wait` (on by default), `-t/--timeout` (30s) |
| `stop` | `-c/--cluster`, `-m/--mode` Smart\|Fast\|Immediate (Smart), `-w/--wait`, `-t/--timeout` (180s) |
| `restart` | `-c/--cluster`, `-m/--mode` (Fast), `-t/--timeout` (30s) |
| `reload` | `-c/--cluster` |
| `cluster add` | `-p/--port` (omit = auto), `--port-start` (5500), `-d/--data-directory`, `-u/--superuser`, `--host`, `--encoding`, `--locale`, `--no-init` |
| `cluster remove` | `--keep-data`, `-f/--force` |
| `config set` / `get` / `unset` / `list` | `-c/--cluster` |
| `sql` | `-s/--sql`, `-f/--file`, `-c/--cluster`, `-d/--database`, `-u/--user` |
| `db list` | `-c/--cluster` |
| `dump` | `-c/--cluster`, `-d/--database`, `-F/--format` p\|c\|d\|t, `--schema-only`, `--data-only`, `-j/--jobs` |
| `restore` | `-c/--cluster`, `-d/--database`, `-F/--format` c\|d\|t, `--clean`, `--create`, `--exit-on-error`, `-j/--jobs` |
| `archive` | `-c/--cluster`, `-m/--mode` (Fast) |
| `listen` | `-c/--cluster` |
| `hba allow` | `-m/--method` (scram-sha-256), `-u/--user`, `-d/--database`, `-t/--type`, `--map`, `-c/--cluster` |
| `hba harden` | `-m/--method` (scram-sha-256), `-c/--cluster` |
| `role add` | `--password-stdin`, `--superuser`, `--createdb`, `--login`, `-c/--cluster` |
| `uri` | `-c/--cluster`, `-u/--user`, `-d/--database`, `--host` |

Run `empg <command> --help` for the authoritative list.

### Running SQL

`empg sql` takes its statement three ways, exactly one at a time:

```bash
empg sql "SELECT version()"                 # positional
empg sql -s "SELECT version()"              # -s/--sql, the same thing
empg sql -f schema.sql -d mydb              # a script
```

Note that `-c` is `--cluster` here, not psql's `--command`; use `-s` if that is the habit you are
carrying over. Passing more than one of the three, or none, is an error.

Scripts stop at the first failing statement and the command exits non-zero, so a broken migration
cannot report success. Errors from the server are printed as psql emits them:

```
$ empg sql -f broken.sql
psql:broken.sql:1: ERROR:  division by zero
$ echo $?
1
```

### Using an existing PostgreSQL installation

If PostgreSQL is already unpacked somewhere — a vendored or shared copy — `instance adopt` uses it
in place instead of downloading anything:

```bash
empg instance adopt "C:\downloads\pg\instance" \
  --name projectbase --port 55432 --data-directory "D:\pgdata\primary"
```

The directory must already contain `bin/pg_ctl`, `bin/initdb` and `bin/postgres`; the version is
read from those binaries rather than assumed. The instance is registered under `--name`, so the
path never has to be typed again. `adopt` has no reinstall or force option — it never writes to
the installation.

`--data-directory` accepts an absolute path, so cluster data can live outside a read-only or
centrally managed installation:

```bash
empg -i projectbase cluster add reporting --data-directory "D:\pgdata\reporting"
empg instance use projectbase      # then even -i becomes unnecessary
empg cluster list
```

### Remote access

Reaching a cluster from another machine takes two independent things, and missing either one looks
like a connection problem rather than a configuration one:

1. **Binding** — `listen_addresses` decides which interfaces the server accepts sockets on. The
   default is loopback only, and `--host` does *not* change it: that option is the address handed
   to clients, not the one the server binds.
2. **Authentication** — `pg_hba.conf` decides who may connect from where. Widening the binding
   without a matching rule still refuses every remote connection.

```bash
empg listen "localhost,192.168.1.10"        # bind; needs a restart
empg restart
printf '%s' "$SECRET" | empg role password postgres --password-stdin
empg hba allow 192.168.1.0/24 --method scram-sha-256
empg reload                                  # pg_hba is re-read on reload
```

`empg hba list` prints the rules in the order PostgreSQL evaluates them. That order matters:
matching stops at the first rule that applies, so a broad rule above a narrow one answers for
connections the narrow one was written for. empg keeps its own managed block sorted narrowest
first so that adding a broad rule later cannot shadow a specific one.

Rules empg manages live between markers in `pg_hba.conf`; anything you write outside that block is
left alone, and re-running a command does not duplicate entries.

### Authentication and passwords

New clusters come from initdb with `trust` rules, which accept any client claiming a role name
without a password. `empg hba harden` replaces them with `scram-sha-256`:

```bash
printf '%s' "$SECRET" | empg role password postgres --password-stdin
empg hba harden
```

The order is enforced, not advisory: `harden` refuses to run until the superuser has a password,
because under `trust` a password is accepted but not required — set it first and the same
connection keeps working afterwards; drop trust first and there is no way back in.

Passwords are only ever read from standard input. There is no `--password` option, because
arguments are visible to every user on the machine through the process list and are usually
recorded in shell history. Statements carrying a password are executed from a temporary file so
they never appear in `psql`'s command line either.

After hardening, empg's own commands need the password too — export `PGPASSWORD` for them.

### What destroy deletes

Removing a PostgreSQL installation is always opt-in:

```bash
empg instance destroy            # stop clusters, delete their data, remove .empg/ — binaries untouched
empg instance destroy --purge    # the above, and delete the installation directory too
```

Being opt-in is deliberate, and is not decided by the instance's kind alone. The kind is recorded
in the manifest, but a manifest can be hand-edited, restored from a backup, or written by an older
release that predates the field — and a missing field would read as "managed", the permissive
answer. No irreversible delete is staked on that. The default destroys only what empg can always
identify as its own: cluster data and its own state directory.

The kind is then a second guard on top: `--purge` is refused outright on an adopted instance. And
if a manifest is missing or unreadable, no command runs at all, so nothing can be deleted on the
strength of bad state.

An instance can host several clusters, each with its own port and data directory:

```bash
empg cluster add reporting --port 55433
empg start --cluster reporting
empg config set shared_buffers 256MB --cluster reporting
empg reload
```

Commands that take a cluster default to the only one when the instance has just one, and require
`--cluster` when it has more.

State the CLI needs but the library does not persist — cluster ids, ports, superusers and
parameters — is kept in `.empg/manifest.json` inside the instance directory.

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

### Example of creating a data cluster and restoring database from an existing dump.
```csharp
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
    options.Source = Path.Combine("downloads", "dvdrental.tar");
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
