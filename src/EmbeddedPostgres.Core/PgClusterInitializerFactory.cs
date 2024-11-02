using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Core.Services;
using System;

namespace EmbeddedPostgres;

/// <summary>
/// Provides a factory for creating different types of initialization sources for PostgreSQL data clusters.
/// This class offers methods to configure and initialize a data cluster either by setting up a new database
/// or by restoring from an archive.
/// </summary>
/// <param name="environment">The environment configuration used for initializing the data cluster.</param>
public class PgClusterInitializerFactory(PgEnvironment environment)
{
    /// <summary>
    /// Creates a compound cluster initializer that chains multiple initializers to be executed in sequence.
    /// </summary>
    /// <param name="clusterInitializers">An array of initializers to chain together.</param>
    /// <returns>A compound initializer that executes each provided initializer in order.</returns>
    public IPgClusterInitializer Chain(params IPgClusterInitializer[] clusterInitializers) 
        => new PgCompoundInitializer(environment, clusterInitializers);

    /// <summary>
    /// Creates a new initialization source for setting up the PostgreSQL data cluster using InitDbController with the provided options.
    /// </summary>
    /// <param name="options">The options that configure how InitDbController initializes the PostgreSQL cluster.</param>
    /// <returns>An implementation of <see cref="IPgClusterInitializer"/> that will initialize the cluster using InitDbController.</returns>
    public IPgClusterInitializer InitializeUsingInitDb(PgInitDbOptions options = null)
        => new PgInitDbInitializer(environment, options ?? PgInitDbOptions.Default);

    /// <summary>
    /// Creates a new initialization source for setting up the PostgreSQL data cluster using InitDbController
    /// with options configured by the specified <paramref name="configurer"/> action.
    /// </summary>
    /// <param name="configurer">A delegate that configures <see cref="PgInitDbOptions"/> to customize InitDbController initialization.</param>
    /// <returns>An implementation of <see cref="IPgClusterInitializer"/> that will initialize the cluster using InitDbController.</returns>
    public IPgClusterInitializer InitializeUsingInitDb(Action<PgInitDbOptions> configurer)
    {
        var options = PgInitDbOptions.Default;
        configurer(options);
        return new PgInitDbInitializer(environment, options);
    }

    /// <summary>
    /// Creates a new initialization source for restoring a PostgreSQL data cluster from an archive with the provided options.
    /// </summary>
    /// <param name="options">The options that configure how the restoration from the archive will be carried out.</param>
    /// <returns>An implementation of <see cref="IPgClusterInitializer"/> that will restore the cluster from the provided archive.</returns>
    public IPgClusterInitializer RestoreFromArchive(PgRestoreArchiveOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.ArchiveFilePath, nameof(options.ArchiveFilePath));
        return new PgRestoreArchiveInitializer(environment, options);
    }

    /// <summary>
    /// Creates a new initialization source for restoring a PostgreSQL data cluster from an archive
    /// with options configured by the specified <paramref name="configurer"/> action.
    /// </summary>
    /// <param name="configurer">A delegate that configures <see cref="PgRestoreArchiveOptions"/> to customize archive restoration.</param>
    /// <returns>An implementation of <see cref="IPgClusterInitializer"/> that will restore the cluster from the provided archive.</returns>
    public IPgClusterInitializer RestoreFromArchive(Action<PgRestoreArchiveOptions> configurer)
    {
        PgRestoreArchiveOptions options = new();
        configurer(options);
        return RestoreFromArchive(options);
    }

    /// <summary>
    /// Creates a new instance of <see cref="PgClusterInitializerFactory"/> for the given environment configuration.
    /// </summary>
    /// <param name="environment">The environment configuration for PostgreSQL cluster initialization.</param>
    /// <returns>An instance of <see cref="PgClusterInitializerFactory"/> for the provided environment.</returns>
    public static PgClusterInitializerFactory FromEnvironment(PgEnvironment environment)
        => new PgClusterInitializerFactory(environment);
}
