using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Shared behaviour for the two ways an instance comes into existence.
/// </summary>
/// <remarks>
/// An instance is either <em>managed</em> — empg downloaded and unpacked its PostgreSQL binaries
/// and may therefore delete them — or <em>adopted</em>, meaning the binaries were already on disk
/// and belong to someone else. Everything after that point is identical, so the difference is
/// expressed as two commands rather than a flag, and the distinction is recorded in the manifest
/// where destructive commands can consult it.
/// </remarks>
public abstract class InstanceProvisionCommandBase : EmpgCommandBase
{
    /// <summary>
    /// Number of executables <see cref="IPgEnvironmentBuilder.ValidateAsync"/> reports for a
    /// usable instance: pg_ctl, initdb and postgres.
    /// </summary>
    protected const int RequiredBinaryCount = 3;

    private readonly IEmpgContextResolver contextResolver;
    private readonly IPgEnvironmentBuilder environmentBuilder;

    protected InstanceProvisionCommandBase(
        IEmpgContextResolver contextResolver,
        IPgEnvironmentBuilder environmentBuilder)
    {
        this.contextResolver = contextResolver;
        this.environmentBuilder = environmentBuilder;
    }

    [CommandParameter(0, Name = "path", Description = "Instance directory. Defaults to the current directory.")]
    public string? InstancePath { get; set; }

    [CommandOption("name", 'n', Description = "Register the instance under this name. Defaults to the directory name.")]
    public string? Name { get; set; }

    [CommandOption("no-register", Description = "Do not add the instance to the registry.")]
    public bool NoRegister { get; set; }

    [CommandOption("cluster", 'c', Description = "Name of the initial data cluster.")]
    public string Cluster { get; set; } = "primary";

    [CommandOption("port", 'p', Description = "Port for the initial cluster. Pass 0 to pick the first free port at or above --port-start.")]
    public int Port { get; set; } = 5432;

    [CommandOption("port-start", Description = "Where automatic port selection begins when --port is 0.")]
    public int PortRangeStart { get; set; } = 5500;

    [CommandOption("data-directory", 'd', Description = "Data directory for the initial cluster. May be an absolute path outside the instance.")]
    public string? DataDirectory { get; set; }

    [CommandOption("superuser", 'u', Description = "Superuser name for the initial cluster.")]
    public string Superuser { get; set; } = "postgres";

    [CommandOption("listen", 'l', Description = "Addresses the server binds to (listen_addresses), for example \"localhost,192.168.1.10\" or \"*\".")]
    public string? Listen { get; set; }

    [CommandOption("durable", Description = "Run with fsync enabled. Turn off only for throwaway clusters.")]
    public bool Durable { get; set; } = true;

    [CommandOption("encoding", Description = "Encoding for the initial cluster.")]
    public string Encoding { get; set; } = "UTF-8";

    [CommandOption("locale", Description = "Locale for the initial cluster.")]
    public string? Locale { get; set; }

    [CommandOption("bare", Description = "Set up the instance without creating an initial cluster.")]
    public bool Bare { get; set; }

    /// <summary>Whether the binaries belong to someone other than empg.</summary>
    protected abstract bool IsAdopted { get; }

    /// <summary>Word used in progress and completion messages.</summary>
    protected abstract string Verb { get; }

    /// <summary>
    /// Makes the PostgreSQL binaries available in the instance directory, or verifies that they
    /// already are, and records what they are in the manifest.
    /// </summary>
    protected abstract Task PrepareBinariesAsync(EmpgContext context, OutputWriter output, CancellationToken cancellationToken);

    /// <summary>Rejects option combinations the concrete command cannot honour.</summary>
    protected virtual void Validate()
    {
    }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var instanceDirectory = contextResolver.ResolveForInit(Directory, InstancePath);

        Validate();

        var manifest = new EmpgManifest { Adopted = IsAdopted };
        var context = new EmpgContext(instanceDirectory, manifest);

        if (!Bare)
        {
            manifest.Clusters.Add(new EmpgClusterEntry
            {
                Id = Cluster,
                DataDirectory = DataDirectory ?? "data",
                Port = Port > 0 ? Port : Helpers.GetAvailablePort(PortRangeStart),
                Superuser = Superuser,
                ListenAddresses = Listen,
                Durable = Durable,
                Encoding = Encoding,
                Locale = Locale,
            });
        }

        await PrepareBinariesAsync(context, output, cancellationToken).ConfigureAwait(false);

        // The manifest is only written once the binaries are confirmed present, so a failed run
        // does not leave a directory that later commands would treat as a valid instance.
        context.Save();

        if (!Bare)
        {
            await InitialiseClusterAsync(context, output, cancellationToken).ConfigureAwait(false);
        }

        var registeredAs = NoRegister ? null : Register(instanceDirectory);

        await output.JsonAsync(new
        {
            instance = instanceDirectory,
            name = registeredAs,
            kind = IsAdopted ? "adopted" : "managed",
            postgresVersion = manifest.PostgresVersion,
            artifact = manifest.ServerArtifact,
            clusters = manifest.Clusters.Select(c => new { c.Id, c.Port, c.Superuser, c.DataDirectory }),
        }).ConfigureAwait(false);

        await output.SuccessAsync($"{Verb} empg instance in {instanceDirectory}").ConfigureAwait(false);

        if (registeredAs is not null)
        {
            await output.InfoAsync($"Registered as '{registeredAs}'. Refer to it with --instance {registeredAs}.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Confirms the instance directory holds a usable PostgreSQL installation and records its
    /// version, taken from the binaries themselves rather than assumed.
    /// </summary>
    protected async Task<Dictionary<string, string>> ValidateBinariesAsync(
        EmpgContext context,
        CancellationToken cancellationToken)
    {
        var binaries = await environmentBuilder
            .ValidateAsync(context.InstanceDirectory, cancellationToken)
            .ConfigureAwait(false);

        context.Manifest.PostgresVersion = ExtractVersion(binaries);
        return binaries;
    }

    private async Task InitialiseClusterAsync(EmpgContext context, OutputWriter output, CancellationToken cancellationToken)
    {
        await output.InfoAsync($"Initialising cluster '{Cluster}' ...").ConfigureAwait(false);

        var environment = await environmentBuilder
            .BuildAsync(PgInstanceConfiguration.NamedInstance(context.InstanceDirectory), cancellationToken)
            .ConfigureAwait(false);

        environment.DataClusters.Add(context.ToClusterConfiguration(context.Manifest.Clusters[0]));

        var server = new PgServer(environment);
        var initializer = PgClusterInitializerFactory.FromEnvironment(environment);

        await server.InitializeAsync(
            initializer: _ => initializer.InitializeUsingInitDb(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the instance to the registry, returning the name it was registered under.
    /// </summary>
    /// <remarks>
    /// Registration is a convenience, so a name already taken by a different directory is reported
    /// rather than allowed to fail an otherwise successful provision.
    /// </remarks>
    private string? Register(string instanceDirectory)
    {
        var name = InstanceName.Require(Name ?? new DirectoryInfo(instanceDirectory).Name);
        var registry = EmpgRegistry.Load();

        var existing = registry.Find(name);
        if (existing is not null)
        {
            return string.Equals(Path.GetFullPath(existing.Path), instanceDirectory, StringComparison.OrdinalIgnoreCase)
                ? existing.Name
                : null;
        }

        registry.Instances.Add(new EmpgRegistryEntry { Name = name, Path = instanceDirectory });
        registry.Active ??= name;
        registry.Save();

        return name;
    }

    /// <summary>
    /// Pulls the version out of a banner such as "postgres (PostgreSQL) 17.10".
    /// </summary>
    private static string? ExtractVersion(Dictionary<string, string> binaries)
    {
        if (!binaries.TryGetValue("postgres", out var banner) || string.IsNullOrWhiteSpace(banner))
        {
            return null;
        }

        var parts = banner.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[^1] : null;
    }
}
