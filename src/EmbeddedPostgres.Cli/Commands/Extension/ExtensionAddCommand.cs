using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Extension;

/// <summary>
/// Downloads an extension and unpacks it over the instance.
/// </summary>
[Command("extension add", Description = "Install a PostgreSQL extension into the instance.")]
public partial class ExtensionAddCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly PgServerBuilder serverBuilder;

    public ExtensionAddCommand(IEmpgContextResolver contextResolver, PgServerBuilder serverBuilder)
    {
        this.contextResolver = contextResolver;
        this.serverBuilder = serverBuilder;
    }

    [CommandParameter(0, Name = "source", Description = "URL or local path of the extension archive.")]
    public required string Source { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var context = contextResolver.Resolve(Selector);

        if (context.Manifest.Extensions.Any(e => string.Equals(e, Source, StringComparison.OrdinalIgnoreCase)))
        {
            throw new EmpgException($"Extension '{Source}' is already installed.");
        }

        await output.InfoAsync($"Installing extension {Source} ...").ConfigureAwait(false);

        // Reinstalling over the existing instance is what unpacks the extension alongside the
        // server binaries. CleanInstall stays false so existing cluster data is left alone.
        await serverBuilder.BuildAsync(
            options =>
            {
                options.InstanceDirectory = context.InstanceDirectory;
                options.CacheDirectory = context.CacheDirectory;
                options.CleanInstall = false;

                if (!string.IsNullOrWhiteSpace(context.Manifest.ServerArtifact))
                {
                    options.ServerBinaries = context.Manifest.ServerArtifact;
                }

                foreach (var existing in context.Manifest.Extensions)
                {
                    options.AddPostgresExtension(existing);
                }

                options.AddPostgresExtension(Source);
            },
            cancellationToken).ConfigureAwait(false);

        context.Manifest.Extensions.Add(Source);
        context.Save();

        await output.JsonAsync(new { instance = context.InstanceDirectory, extension = Source }).ConfigureAwait(false);
        await output.SuccessAsync($"Installed extension {Source}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg restart` and `CREATE EXTENSION` in your database to enable it.").ConfigureAwait(false);
    }
}
