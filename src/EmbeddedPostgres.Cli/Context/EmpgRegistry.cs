using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// A registered instance: a name and where it lives.
/// </summary>
public class EmpgRegistryEntry
{
    public required string Name { get; set; }

    /// <summary>Absolute path of the instance directory.</summary>
    public required string Path { get; set; }
}

/// <summary>
/// The user's named instances and which one commands act on by default.
/// </summary>
/// <remarks>
/// This lives with the user rather than inside any instance, so that names resolve from any
/// working directory. It holds nothing but names and paths: everything describing an instance
/// stays in that instance's own manifest, so losing this file costs you the names, not the data.
/// </remarks>
public class EmpgRegistry
{
    public const string FileName = "instances.json";

    /// <summary>Overrides where the registry is stored. Mainly useful for testing.</summary>
    public const string HomeVariable = "EMPG_HOME";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public int Version { get; set; } = 1;

    /// <summary>Name of the instance used when no other selection applies.</summary>
    public string? Active { get; set; }

    public List<EmpgRegistryEntry> Instances { get; set; } = new();

    public EmpgRegistryEntry? Find(string name)
        => Instances.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Directory holding the registry: <c>EMPG_HOME</c> when set, otherwise a per-user
    /// application-data directory.
    /// </summary>
    public static string GetHomeDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return System.IO.Path.GetFullPath(overridden);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            // Falls back to the user's profile on systems that report no application-data folder.
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return System.IO.Path.Combine(appData, "empg");
    }

    public static string GetRegistryPath()
        => System.IO.Path.Combine(GetHomeDirectory(), FileName);

    public static EmpgRegistry Load()
    {
        var path = GetRegistryPath();
        if (!File.Exists(path))
        {
            return new EmpgRegistry();
        }

        try
        {
            return JsonSerializer.Deserialize<EmpgRegistry>(File.ReadAllText(path), SerializerOptions)
                ?? new EmpgRegistry();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            throw new EmpgException($"The instance registry at {path} could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes the registry, replacing any existing copy atomically.
    /// </summary>
    public void Save()
    {
        var path = GetRegistryPath();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, SerializerOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
