using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Extensions;

namespace EmbeddedPostgres.Extensions;

public static class PgDataClusterExtensions
{
    /// <summary>
    /// Returns the full path of the instance directory specified in the <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The <see cref="PgDataCluster"/> instance containing the directory details.</param>
    /// <returns>
    /// A string representing the full path to the instance directory specified in <paramref name="configuration"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetInstanceFullPath(this PgDataCluster cluster)
        => cluster.Environment.Instance.GetInstanceFullPath();

    /// <summary>
    /// Returns the full path of the data/database directory. 
    /// </summary>
    /// <returns>
    /// A string representing the full path to the data/database directory. The full path is constructed by combining
    /// the <see cref="PgInstanceConfiguration.InstanceDirectory"/> with the 
    /// <see cref="PgDataClusterConfiguration.DataDirectory"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetDataFullPath(this PgDataCluster cluster)
        => cluster.Environment.Instance.GetDataFullPath(cluster.Settings);
}