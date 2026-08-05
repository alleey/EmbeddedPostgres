using CliFx.Binding;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Adopts a PostgreSQL installation that is already on disk.
/// </summary>
/// <remarks>
/// Nothing is downloaded and nothing is overwritten: the installation may be vendored, shared or
/// centrally managed, so empg records that it does not own the binaries and will not delete them.
/// There is deliberately no force or reinstall option here — that is what
/// <c>instance create</c> is for.
/// </remarks>
[Command("instance adopt", Description = "Adopt a PostgreSQL installation already on disk.")]
public partial class InstanceAdoptCommand : InstanceProvisionCommandBase
{
    public InstanceAdoptCommand(
        IEmpgContextResolver contextResolver,
        IPgEnvironmentBuilder environmentBuilder)
        : base(contextResolver, environmentBuilder)
    {
    }

    protected override bool IsAdopted => true;

    protected override string Verb => "Adopted";

    protected override async Task PrepareBinariesAsync(EmpgContext context, OutputWriter output, CancellationToken cancellationToken)
    {
        if (!System.IO.Directory.Exists(context.InstanceDirectory))
        {
            throw new EmpgException($"{context.InstanceDirectory} does not exist.");
        }

        if (File.Exists(EmpgManifest.GetManifestPath(context.InstanceDirectory)))
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} is already an empg instance. " +
                "Register it under a name with `empg instance add <name> <path>` instead.");
        }

        await output.InfoAsync($"Adopting PostgreSQL binaries in {context.InstanceDirectory} ...").ConfigureAwait(false);

        Dictionary<string, string> binaries;
        try
        {
            binaries = await ValidateBinariesAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (PgCoreException ex)
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} does not contain a usable PostgreSQL installation: {ex.Message}");
        }

        if (binaries.Count != RequiredBinaryCount)
        {
            throw new EmpgException(
                $"{context.InstanceDirectory} does not contain a usable PostgreSQL installation: " +
                $"found {binaries.Count} of {RequiredBinaryCount} required binaries. " +
                "Expected pg_ctl, initdb and postgres under a 'bin' subdirectory.");
        }

        // Nothing was fetched, so there is no artifact to record.
        context.Manifest.ServerArtifact = null;
    }
}
