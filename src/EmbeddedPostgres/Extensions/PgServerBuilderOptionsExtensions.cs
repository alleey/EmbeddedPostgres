using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EmbeddedPostgres.Extensions;

public static class PgServerBuilderOptionsExtensions
{
    internal static PgServerBuilderOptions Build(this PgServerBuilderOptions options)
    {
        if (options.NormallizeAttributes)
        {
            options.AddInstanceParameter(PgKnownParameters.NormallizeAttributes, true);
        }
        if (options.AddLocalUserAccessPermission)
        {
            options.AddInstanceParameter(PgKnownParameters.Windows.AddLocalUserAccessPermission, true);
        }
        if (options.SetExecutableAttributes)
        {
            options.AddInstanceParameter(PgKnownParameters.Linux.SetExecutableAttributes, true);
        }
        return options;
    }

    /// <summary>
    /// Validates the provided <see cref="PgServerBuilderOptions"/> to ensure they are correct and meet the required criteria.
    /// </summary>
    /// <param name="serverOptions">The options to validate for the PostgreSQL server builder.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serverOptions"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the options in <paramref name="serverOptions"/> are invalid.</exception>
    public static void Validate(this PgServerBuilderOptions serverOptions)
    {
        if (serverOptions.DataClusters.Count == 0)
        {
            throw new PgValidationException("A data cluster must be configured for the server");
        }
        serverOptions.ValidateDataClusterOptions();
        //serverOptions.dataClusters.Add(options);
    }

    /// <summary>
    /// Validates the data clusters configured in the provided <see cref="PgServerBuilderOptions"/> instance.
    /// Ensures that each data cluster has a valid port, unique IDs, and non-duplicate configurations.
    /// Throws an <see cref="PgValidationException"/> if any validation fails.
    /// </summary>
    /// <param name="serverOptions">
    /// The <see cref="PgServerBuilderOptions"/> instance containing the data clusters to validate.
    /// </param>
    /// <exception cref="PgValidationException">
    /// Thrown when any of the following conditions are met:
    /// <list type="bullet">
    /// <item><description>One or more data clusters have a port value of 0.</description></item>
    /// <item><description>There are duplicate data cluster IDs.</description></item>
    /// <item><description>There are multiple data clusters with identical configurations (Port, Host, and DataDirectory).</description></item>
    /// </list>
    /// </exception>
    internal static void ValidateDataClusterOptions(this PgServerBuilderOptions serverOptions)
    {
        var badPorts = serverOptions.dataClusters
           .Where(x => x.Port == 0)
           .Select(x => x.UniqueId);

        if (badPorts.Any())
        {
            throw new PgValidationException(
                $"Must specify a valid port for cluster: {string.Join(',', badPorts)}");
        }

        var duplicateIds = serverOptions.dataClusters
            .GroupBy(x => x.UniqueId)
            .Where(x => x.Count() > 1)
            .Select(x => x.First())
            .Select(x => x.UniqueId);

        if (duplicateIds.Any())
        {
            throw new PgValidationException(
                $"Cannot have multiple clusterns with the same UniqueIds. Duplicate ids: {string.Join(',', duplicateIds)}");
        }

        var duplicateCoonfigs = serverOptions.dataClusters
            .GroupBy(x => (x.Port, x.Host, x.DataDirectory))
            .Where(x => x.Count() > 1)
            .Select(x => x.First())
            .Select(x => x.UniqueId);

        if (duplicateCoonfigs.Any())
        {
            throw new PgValidationException(
                $"Cannot have multiple clusterns with the same configuration. Duplicate configs: {string.Join(',', duplicateIds)}");
        }
    }
}