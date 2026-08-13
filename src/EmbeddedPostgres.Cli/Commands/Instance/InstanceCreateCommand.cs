using CliFx.Binding;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Creates a managed instance by downloading and unpacking PostgreSQL.
/// </summary>
/// <remarks>
/// Managed means empg installed the binaries and may remove them again, which is what makes
/// <c>instance destroy --purge</c> available here and not for adopted instances.
/// </remarks>
[Command("instance create", Description = "Create an instance, installing PostgreSQL binaries.")]
public partial class InstanceCreateCommand : InstanceProvisionCommandBase
{
    private readonly IPgInstanceBuilder instanceBuilder;

    public InstanceCreateCommand(
        IEmpgContextResolver contextResolver,
        IPgInstanceBuilder instanceBuilder,
        IPgEnvironmentBuilder environmentBuilder)
        : base(contextResolver, environmentBuilder)
    {
        this.instanceBuilder = instanceBuilder;
    }

    [CommandOption("artifact", 'a', Description = "URL or local path of the PostgreSQL binaries.")]
    public string? Artifact { get; set; }

    [CommandOption("pg-version", Description = "PostgreSQL major version to install, for example 18 or 17. Defaults to the newest supported.")]
    public string? PgVersion { get; set; }

    [CommandOption("minimal", 'm', Description = "Use the minimal (Zonky) binaries instead of the standard distribution.")]
    public bool Minimal { get; set; }

    [CommandOption("force", 'f', Description = "Reinstall over an existing instance, discarding its data.")]
    public bool Force { get; set; }

    protected override bool IsAdopted => false;

    protected override string Verb => "Created";

    protected override void Validate()
    {
        if (!string.IsNullOrWhiteSpace(Artifact) && Minimal)
        {
            throw new EmpgException("--artifact names the binaries to install, so --minimal does not also apply.");
        }

        if (!string.IsNullOrWhiteSpace(PgVersion) && !string.IsNullOrWhiteSpace(Artifact))
        {
            throw new EmpgException("--artifact already names the binaries, so --pg-version does not also apply.");
        }

        if (!string.IsNullOrWhiteSpace(PgVersion) && Minimal)
        {
            throw new EmpgException("--pg-version selects a standard distribution build, so --minimal does not also apply.");
        }
    }

    /// <summary>
    /// Downloads and unpacks the PostgreSQL binaries.
    /// </summary>
    /// <remarks>
    /// This goes through <see cref="IPgInstanceBuilder"/> rather than <see cref="PgServerBuilder"/>
    /// because the latter requires at least one data cluster, which would rule out <c>--bare</c>.
    /// </remarks>
    protected override async Task PrepareBinariesAsync(EmpgContext context, OutputWriter output, CancellationToken cancellationToken)
    {
        if (File.Exists(EmpgManifest.GetManifestPath(context.InstanceDirectory)) && !Force)
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} is already an empg instance. Use --force to reinstall it, discarding its data.");
        }

        var artifact = ResolveArtifact();

        await output.InfoAsync($"Installing PostgreSQL binaries into {context.InstanceDirectory} ...").ConfigureAwait(false);

        var options = new PgInstanceBuilderOptions
        {
            InstanceDirectory = context.InstanceDirectory,
            CleanInstall = Force,
            ExcludePgAdminInstallation = true,
        };

        // Mirrors the platform fixes PgServerBuilder applies; without this the extracted
        // executables are not marked executable on Linux.
        options.PlatformParameters[PgKnownParameters.Linux.SetExecutableAttributes] = true;

        var installationSource = new PgInstallationSource(context.CacheDirectory);
        installationSource.UseMain(artifact);

        await instanceBuilder
            .BuildAsync(options, installationSource.Build(), cancellationToken)
            .ConfigureAwait(false);

        await ValidateBinariesAsync(context, cancellationToken).ConfigureAwait(false);
        context.Manifest.ServerArtifact = artifact.Source;
    }

    private PgArtifact ResolveArtifact()
    {
        if (!string.IsNullOrWhiteSpace(Artifact))
        {
            return PgCustomBinaries.Artifact(Artifact);
        }

        if (!string.IsNullOrWhiteSpace(PgVersion))
        {
            try
            {
                return PgStandardBinaries.WebArtifact(PgVersion, PgPlatform.Current);
            }
            catch (NotSupportedException ex)
            {
                // A mistyped --pg-version is user error, not a crash: surface it the
                // way the other option validation does, without a stack trace.
                throw new EmpgException(ex.Message);
            }
        }

        return Minimal ? PgIoZonkyTestBinaries.Latest() : PgStandardBinaries.Latest();
    }
}
