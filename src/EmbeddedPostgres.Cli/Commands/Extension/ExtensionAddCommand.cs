using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Cli.Commands.Extension;

/// <summary>
/// Downloads an extension and unpacks it over the instance.
/// </summary>
[Command("extension add", Description = "Install a PostgreSQL extension into the instance.")]
public partial class ExtensionAddCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IPgInstanceBuilder instanceBuilder;

    public ExtensionAddCommand(IEmpgContextResolver contextResolver, IPgInstanceBuilder instanceBuilder)
    {
        this.contextResolver = contextResolver;
        this.instanceBuilder = instanceBuilder;
    }

    [CommandParameter(0, Name = "source", Description = "URL or local path of the extension archive.")]
    public required string Source { get; set; }

    /// <summary>
    /// Unpacks the extension over the existing installation.
    /// </summary>
    /// <remarks>
    /// This goes through <see cref="IPgInstanceBuilder"/> rather than <see cref="PgServerBuilder"/> for two
    /// reasons. The latter requires at least one data cluster, which installing binaries has no need of; and
    /// it treats a working installation as nothing left to do, which silently skips the extension entirely.
    /// <para>
    /// Only the named extension is installed. Extensions already recorded in the manifest are left alone
    /// rather than unpacked again, so adding one does not re-extract every archive added before it.
    /// </para>
    /// </remarks>
    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);

        if (context.Manifest.Extensions.Any(e => string.Equals(e, Source, StringComparison.OrdinalIgnoreCase)))
        {
            throw new EmpgException($"Extension '{Source}' is already installed.");
        }

        await output.InfoAsync($"Installing extension {Source} ...").ConfigureAwait(false);

        var options = new PgInstanceBuilderOptions
        {
            InstanceDirectory = context.InstanceDirectory,
            CleanInstall = false,
        };

        // Mirrors what provisioning applies, so files unpacked here are executable on Linux too.
        options.PlatformParameters[PgKnownParameters.Linux.SetExecutableAttributes] = true;

        var installationSource = new PgInstallationSource(context.CacheDirectory);

        if (PathChecker.IsLocalPath(Source))
        {
            installationSource.UseLocalExtension(Source);
        }
        else
        {
            installationSource.UseWebExtension(Source);
        }

        await instanceBuilder
            .BuildAsync(options, installationSource.Build(), cancellationToken)
            .ConfigureAwait(false);

        // Recorded only once the archive has actually been unpacked, so `extension list` cannot claim
        // an extension that never made it onto disk.
        context.Manifest.Extensions.Add(Source);
        context.Save();

        await output.JsonAsync(new { instance = context.InstanceDirectory, extension = Source }).ConfigureAwait(false);
        await output.SuccessAsync($"Installed extension {Source}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg restart` and `CREATE EXTENSION` in your database to enable it.").ConfigureAwait(false);
    }
}
