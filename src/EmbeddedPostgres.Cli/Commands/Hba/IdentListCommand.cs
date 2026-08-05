using CliFx.Binding;
using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;
using EmbeddedPostgres.Cli.Output;
using EmbeddedPostgres.Core.Configuration;

namespace EmbeddedPostgres.Cli.Commands.Hba;

/// <summary>
/// Lists the principal-to-role mappings in a cluster's <c>pg_ident.conf</c>.
/// </summary>
[Command("ident list", Description = "List OS principal to role mappings.")]
public partial class IdentListCommand : EmpgCommandBase
{
    private readonly IEmpgContextResolver contextResolver;

    public IdentListCommand(IEmpgContextResolver contextResolver)
    {
        this.contextResolver = contextResolver;
    }

    [CommandOption("cluster", 'c', Description = "Cluster to inspect. Required when the instance has more than one.")]
    public string? Cluster { get; set; }

    protected override async ValueTask ExecuteAsync(IConsole console, OutputWriter output)
    {
        var context = contextResolver.Resolve(Selector);
        var entry = context.ResolveCluster(Cluster);
        var path = context.GetClusterFile(entry, PgIdentFile.FileName);

        if (!File.Exists(path))
        {
            throw new EmpgException($"{path} does not exist. The cluster has not been initialised yet.");
        }

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var managed = PgManagedBlock.Read(text).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mappings = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => (Line: line, Columns: line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(entry => entry.Columns.Length >= 3)
            .ToList();

        await output.JsonAsync(new
        {
            cluster = entry.Id,
            file = path,
            mappings = mappings.Select(m => new
            {
                map = m.Columns[0],
                principal = m.Columns[1],
                role = m.Columns[2],
                managed = managed.Contains(m.Line),
            }),
        }).ConfigureAwait(false);

        if (mappings.Count == 0)
        {
            await output.LineAsync("No mappings. Add one with `empg ident add <principal> <role>`.").ConfigureAwait(false);
            return;
        }

        await output.TableAsync(
            ["MAP", "PRINCIPAL", "ROLE", "OWNER"],
            mappings.Select(m => (IReadOnlyList<string>)new[]
            {
                m.Columns[0],
                m.Columns[1],
                m.Columns[2],
                managed.Contains(m.Line) ? "empg" : "manual",
            }).ToList()).ConfigureAwait(false);
    }
}
