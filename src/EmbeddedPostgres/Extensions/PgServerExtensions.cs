using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Utils;

namespace EmbeddedPostgres.Extensions;

public static class PgServerExtensions
{
    /// <summary>
    /// Returns the full path of the instance directory specified in the <paramref name="configuration"/>.
    /// </summary>
    /// <param name="server">The <see cref="PgServer"/> instance containing the directory details.</param>
    /// <returns>
    /// A string representing the full path to the instance directory.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
    public static string GetInstanceFullPath(this PgServer server)
        => server.Environment.GetInstanceFullPath();
}