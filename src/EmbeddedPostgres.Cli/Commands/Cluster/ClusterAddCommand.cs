using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Cli.Commands.Cluster;

/// <summary>
/// Adds a data cluster to the instance and initialises it.
/// </summary>
[Command("cluster add", Description = "Add a data cluster to the instance.")]
public partial class ClusterAddCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IEmpgServerFactory serverFactory;

    public ClusterAddCommand(IEmpgContextResolver contextResolver, IEmpgServerFactory serverFactory)
    {
        this.contextResolver = contextResolver;
        this.serverFactory = serverFactory;
    }

    [CommandParameter(0, Name = "name", Description = "Name of the new cluster.")]
    public required string Name { get; set; }

    [CommandOption("port", 'p', Description = "Port for the cluster. Omit to pick the first free port at or above --port-start.")]
    public int Port { get; set; }

    [CommandOption("port-start", Description = "Where automatic port selection begins when --port is omitted.")]
    public int PortRangeStart { get; set; } = 5500;

    [CommandOption("data-directory", 'd', Description = "Data directory, relative to the instance. Defaults to the cluster name.")]
    public string? DataDirectory { get; set; }

    [CommandOption("superuser", 'u', Description = "Superuser name.")]
    public string Superuser { get; set; } = "postgres";

    [CommandOption("host", Description = "Address clients are told to connect to. Does not affect what the server binds to.")]
    public string Host { get; set; } = "localhost";

    [CommandOption("listen", 'l', Description = "Addresses the server binds to (listen_addresses), for example \"localhost,192.168.1.10\" or \"*\".")]
    public string? Listen { get; set; }

    [CommandOption("durable", Description = "Run with fsync enabled. Turn off only for throwaway clusters.")]
    public bool Durable { get; set; } = true;

    [CommandOption("encoding", Description = "Cluster encoding.")]
    public string Encoding { get; set; } = "UTF-8";

    [CommandOption("locale", Description = "Cluster locale.")]
    public string? Locale { get; set; }

    [CommandOption("no-init", Description = "Register the cluster without running initdb.")]
    public bool NoInit { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);

        if (context.Manifest.FindCluster(Name) is not null)
        {
            throw new EmpgException($"A cluster named '{Name}' already exists.");
        }

        // Probing only rules out ports currently in use, so a cluster registered now and started
        // later can still collide. Ports already claimed by this instance are excluded below.
        var port = Port > 0 ? Port : ChooseFreePort(context);

        var dataDirectory = DataDirectory ?? Name;

        // Two clusters sharing a data directory would corrupt each other.
        var conflict = context.Manifest.Clusters
            .FirstOrDefault(c => string.Equals(c.DataDirectory, dataDirectory, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null)
        {
            throw new EmpgException($"Cluster '{conflict.Id}' already uses data directory '{dataDirectory}'.");
        }

        var portConflict = context.Manifest.Clusters.FirstOrDefault(c => c.Port == port && c.Host == Host);
        if (portConflict is not null)
        {
            throw new EmpgException($"Cluster '{portConflict.Id}' already listens on {Host}:{port}.");
        }

        var entry = new EmpgClusterEntry
        {
            Id = Name,
            DataDirectory = dataDirectory,
            Host = Host,
            ListenAddresses = Listen,
            Durable = Durable,
            Port = port,
            Superuser = Superuser,
            Encoding = Encoding,
            Locale = Locale,
        };

        context.Manifest.Clusters.Add(entry);
        context.Save();

        if (!NoInit)
        {
            await output.InfoAsync($"Initialising cluster '{Name}' ...").ConfigureAwait(false);

            var server = await serverFactory.OpenAsync(context, Name, cancellationToken).ConfigureAwait(false);
            var initializer = PgClusterInitializerFactory.FromEnvironment(server.Environment);

            await server.InitializeAsync(
                initializer: _ => initializer.InitializeUsingInitDb(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await output.JsonAsync(new { cluster = entry }).ConfigureAwait(false);
        await output.SuccessAsync($"Added cluster '{Name}' on {Host}:{port}").ConfigureAwait(false);
    }

    /// <summary>
    /// Picks the first port at or above <see cref="PortRangeStart"/> that is neither in use on the
    /// machine nor already reserved by another cluster in this instance.
    /// </summary>
    /// <remarks>
    /// The library's probe only sees sockets that are currently open, so a registered but stopped
    /// cluster would otherwise be invisible to it and could be handed out twice.
    /// </remarks>
    private int ChooseFreePort(EmpgContext context)
    {
        var reserved = context.Manifest.Clusters.Select(c => c.Port).ToHashSet();

        var candidate = PortRangeStart;
        while (candidate < ushort.MaxValue)
        {
            var free = Helpers.GetAvailablePort(candidate);
            if (!reserved.Contains(free))
            {
                return free;
            }
            candidate = free + 1;
        }

        throw new EmpgException($"No free port found at or above {PortRangeStart}.");
    }
}
