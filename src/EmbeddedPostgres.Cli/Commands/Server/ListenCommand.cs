using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;

namespace EmbeddedPostgres.Cli.Commands.Server;

/// <summary>
/// Sets which interfaces a cluster binds to.
/// </summary>
[Command("listen", Description = "Set the addresses a cluster binds to (listen_addresses).")]
public partial class ListenCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public ListenCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "addresses", Description = "Comma-separated addresses, or \"*\" for every interface. Use \"localhost\" to return to loopback only.")]
    public required string Addresses { get; set; }

    [CommandOption("cluster", 'c', Description = "Cluster to configure. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);

        entry.ListenAddresses = Addresses;
        context.Save();

        await output.JsonAsync(new { cluster = entry.Id, listenAddresses = Addresses }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: listen_addresses = {Addresses}").ConfigureAwait(false);

        // listen_addresses is only read at postmaster start, so a reload will not pick it up.
        await output.InfoAsync("Run `empg restart` to apply.").ConfigureAwait(false);

        if (!IsLoopbackOnly(Addresses))
        {
            await output.WarnAsync(
                "Binding beyond loopback does not by itself permit remote connections: pg_hba.conf still decides. " +
                "Use `empg hba allow <cidr>` to admit them, and make sure the accounts involved have passwords.").ConfigureAwait(false);
        }
    }

    private static bool IsLoopbackOnly(string addresses)
        => addresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(a => a is "localhost" or "127.0.0.1" or "::1");
}
