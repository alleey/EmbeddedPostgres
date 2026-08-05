using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Maps an operating-system principal to a database role in <c>pg_ident.conf</c>.
/// </summary>
/// <remarks>
/// Used with the <c>sspi</c>, <c>gss</c> and <c>peer</c> methods, where the client is identified by
/// the operating system and a mapping decides which database role that identity becomes.
/// </remarks>
[Command("ident add", Description = "Map an OS principal to a database role.")]
public partial class IdentAddCommand : EmpgCommandBase
{
    /// <summary>Default map name, referenced from pg_hba as <c>map=empg</c>.</summary>
    public const string DefaultMap = "empg";

    private readonly IEmpgContextResolver contextResolver;

    public IdentAddCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandParameter(0, Name = "principal", Description = "OS principal, for example DOMAIN\\user or user@REALM.")]
    public required string Principal { get; set; }

    [CommandParameter(1, Name = "role", Description = "Database role the principal becomes.")]
    public required string Role { get; set; }

    [CommandOption("map", Description = "Map name referenced from pg_hba rules.")]
    public string Map { get; set; } = DefaultMap;

    [CommandOption("cluster", 'c', Description = "Cluster to configure. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var role = PgIdentifier.Require(Role, "Role name");
        var path = context.GetClusterFile(entry, PgIdentFile.FileName);

        if (!File.Exists(path))
        {
            throw new EmpgException($"{path} does not exist. The cluster has not been initialised yet.");
        }

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);

        // Principals may contain characters that need quoting; the role never does, having been
        // validated above.
        var principal = Principal.Contains(' ') ? $"\"{Principal}\"" : Principal;
        var line = $"{Map} {principal} {role}";

        // pg_ident accumulates: keep every existing mapping and add this one if new.
        var existing = PgManagedBlock.Read(text).ToList();
        if (existing.Any(l => string.Equals(l, line, StringComparison.OrdinalIgnoreCase)))
        {
            await output.InfoAsync("Mapping already present; nothing changed.").ConfigureAwait(false);
            return;
        }

        existing.Add(line);
        await File.WriteAllTextAsync(path, PgManagedBlock.Write(text, existing)).ConfigureAwait(false);

        await output.JsonAsync(new { cluster = entry.Id, map = Map, principal = Principal, role, file = path }).ConfigureAwait(false);
        await output.SuccessAsync($"{entry.Id}: {Principal} -> {role} (map {Map})").ConfigureAwait(false);
        await output.InfoAsync(
            $"Reference it from a rule, for example: empg hba allow samenet --method sspi --map {Map}").ConfigureAwait(false);
        await output.InfoAsync("Run `empg reload` to apply.").ConfigureAwait(false);
    }
}
