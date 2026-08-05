using System.Text.Json;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// How a command was told which instance to act on.
/// </summary>
public record EmpgSelector(string? Name = null, string? Directory = null);

public interface IEmpgContextResolver
{
    /// <summary>
    /// Locates the instance a command should act on.
    /// </summary>
    EmpgContext Resolve(EmpgSelector selector);

    /// <summary>
    /// Returns the directory a new instance should be created in.
    /// </summary>
    string ResolveForInit(string? explicitDirectory, string? path);
}

/// <summary>
/// Finds the instance a command applies to.
/// </summary>
/// <remarks>
/// Selection runs from the most explicit signal to the most ambient:
/// <list type="number">
/// <item><description>a registered name given with <c>--instance</c>;</description></item>
/// <item><description>a path given with <c>-C</c>;</description></item>
/// <item><description>the <c>EMPG_DIR</c> environment variable;</description></item>
/// <item><description>an instance found by walking up from the working directory;</description></item>
/// <item><description>the active instance from the registry.</description></item>
/// </list>
/// The walk up sits above the active instance deliberately: standing inside an instance is a
/// clearer statement of intent than a default set at some earlier point.
/// </remarks>
public class EmpgContextResolver : IEmpgContextResolver
{
    /// <summary>
    /// Environment variable naming the instance directory, equivalent to <c>GIT_DIR</c>.
    /// </summary>
    public const string DirectoryVariable = "EMPG_DIR";

    public EmpgContext Resolve(EmpgSelector selector)
    {
        var instanceDirectory = Locate(selector);

        EmpgManifest manifest;
        try
        {
            manifest = EmpgManifest.Load(instanceDirectory);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
        {
            throw new EmpgException($"Manifest at {EmpgManifest.GetManifestPath(instanceDirectory)} could not be read: {ex.Message}");
        }

        return new EmpgContext(instanceDirectory, manifest);
    }

    public string ResolveForInit(string? explicitDirectory, string? path)
    {
        // Provisioning targets the path argument when given, otherwise -C, otherwise cwd. It
        // deliberately does not search: an instance is created where you point, not nearby.
        var target = path ?? explicitDirectory ?? System.IO.Directory.GetCurrentDirectory();
        return Path.GetFullPath(target);
    }

    private static string Locate(EmpgSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.Name))
        {
            return FromRegistry(selector.Name);
        }

        if (!string.IsNullOrWhiteSpace(selector.Directory))
        {
            // An explicit path is a directive, not a hint: use it or fail, never search past it.
            var resolved = Path.GetFullPath(selector.Directory);
            return HasManifest(resolved)
                ? resolved
                : throw new EmpgException(
                    $"{resolved} is not an empg instance. Run `empg instance create {resolved}` there, "
                    + $"or `empg instance adopt {resolved}` to use binaries already present.");
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var resolved = Path.GetFullPath(fromEnvironment);
            return HasManifest(resolved)
                ? resolved
                : throw new EmpgException($"{DirectoryVariable} points at {resolved}, which is not an empg instance.");
        }

        var walked = WalkUp();
        if (walked is not null)
        {
            return walked;
        }

        var registry = EmpgRegistry.Load();
        if (!string.IsNullOrWhiteSpace(registry.Active))
        {
            return FromRegistry(registry.Active);
        }

        throw new EmpgException(Describe(registry));
    }

    /// <summary>
    /// Resolves a registered name to its directory, failing clearly when the registration is stale.
    /// </summary>
    private static string FromRegistry(string name)
    {
        var registry = EmpgRegistry.Load();
        var entry = registry.Find(name)
            ?? throw new EmpgException(
                $"No instance named '{name}'. {Available(registry)}");

        var resolved = Path.GetFullPath(entry.Path);
        if (!HasManifest(resolved))
        {
            throw new EmpgException(
                $"Instance '{entry.Name}' is registered at {resolved}, but there is no empg instance there. " +
                $"Re-create it, or drop the registration with `empg instance remove {entry.Name}`.");
        }

        return resolved;
    }

    /// <summary>
    /// Walks from the working directory towards the filesystem root looking for a manifest.
    /// </summary>
    private static string? WalkUp()
    {
        var current = new DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (HasManifest(current.FullName))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return null;
    }

    private static string Describe(EmpgRegistry registry)
    {
        if (registry.Instances.Count == 0)
        {
            return "No empg instance selected and none registered. Create one with `empg instance create`, "
                + "adopt an existing installation with `empg instance adopt <path>`, "
                + "or register an existing instance with `empg instance add <name> <path>`.";
        }

        return "No empg instance selected. " + Available(registry)
            + " Choose one for this command with --instance, or make it the default with `empg instance use <name>`.";
    }

    private static string Available(EmpgRegistry registry)
        => registry.Instances.Count == 0
            ? "No instances are registered."
            : $"Registered: {string.Join(", ", registry.Instances.Select(i => i.Name))}.";

    private static bool HasManifest(string directory)
        => File.Exists(EmpgManifest.GetManifestPath(directory));
}
