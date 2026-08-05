using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

/// <summary>
/// Verifies that the instance's PostgreSQL binaries are present and usable.
/// </summary>
[Command("instance check", Description = "Verify the instance's PostgreSQL binaries.")]
public partial class InstanceCheckCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;
    private readonly IPgEnvironmentBuilder environmentBuilder;

    public InstanceCheckCommand(IEmpgContextResolver contextResolver, IPgEnvironmentBuilder environmentBuilder)
    {
        this.contextResolver = contextResolver;
        this.environmentBuilder = environmentBuilder;
    }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);

        var binaries = await environmentBuilder
            .ValidateAsync(context.InstanceDirectory, console.RegisterCancellationHandler())
            .ConfigureAwait(false);

        // The environment builder reports the three executables an instance needs to be usable.
        const int RequiredBinaryCount = 3;
        var healthy = binaries.Count == RequiredBinaryCount;

        await output.JsonAsync(new
        {
            instance = context.InstanceDirectory,
            healthy,
            binaries,
        }).ConfigureAwait(false);

        if (!healthy)
        {
            throw new EmpgException(
                $"Instance at {context.InstanceDirectory} is incomplete: found {binaries.Count} of {RequiredBinaryCount} required binaries. " +
                "Reinstall with `empg instance create --force`.");
        }

        await output.TableAsync(
            ["BINARY", "VERSION"],
            binaries.Select(b => (IReadOnlyList<string>)new[] { b.Key, b.Value }).ToList()).ConfigureAwait(false);

        await output.SuccessAsync($"Instance at {context.InstanceDirectory} is healthy.").ConfigureAwait(false);
    }
}
