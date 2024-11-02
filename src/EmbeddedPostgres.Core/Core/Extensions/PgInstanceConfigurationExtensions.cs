using EmbeddedPostgres.Core.Interfaces;
using System.IO;

namespace EmbeddedPostgres.Core.Extensions;

public static class PgInstanceConfigurationExtensions
{
    /// <summary>
    /// Returns the full path of the instance directory specified in the <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The <see cref="PgInstanceConfiguration"/> instance containing the directory details.</param>
    /// <returns>
    /// A string representing the full path to the instance directory specified in <paramref name="configuration"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetInstanceFullPath(this PgInstanceConfiguration configuration)
        => Path.GetFullPath(configuration.InstanceDirectory);

    /// <summary>
    /// Returns the full path of the data/database directory.
    /// </summary>
    /// <param name="configuration">The <see cref="PgInstanceConfiguration"/> instance containing configuration details.</param>
    /// <param name="dataCluster">The <see cref="PgDataClusterConfiguration"/> instance containing data cluster details.</param>
    /// <returns>
    /// A string representing the full path to the data/database directory. The full path is constructed by combining
    /// the <see cref="PgInstanceConfiguration.InstanceDirectory"/> with <see cref="PgDataClusterConfiguration.DataDirectory"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetDataFullPath(this PgInstanceConfiguration configuration, PgDataClusterConfiguration dataCluster)
        => configuration.GetDataFullPath(dataCluster.DataDirectory);

    /// <summary>
    /// Returns the full path of the data/database directory.
    /// </summary>
    /// <param name="configuration">The <see cref="PgInstanceConfiguration"/> instance containing configuration details.</param>
    /// <param name="dataDirectory">Data cluster directory.</param>
    /// <returns>
    /// A string representing the full path to the data/database directory. The full path is constructed by combining
    /// the <see cref="PgInstanceConfiguration.InstanceDirectory"/> with <paramref name="dataDirectory"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetDataFullPath(this PgInstanceConfiguration configuration, string dataDirectory)
        => Path.GetFullPath(
            Path.Combine(
                configuration.InstanceDirectory,
                dataDirectory
            )
        );
}