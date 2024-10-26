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
public class PgInitializationSourceFactory(PgEnvironment environment)
{
    /// <summary>
    /// Creates a new initialization source for setting up the PostgreSQL data cluster using InitDb with the provided options.
    /// </summary>
    /// <param name="options">The options that configure how InitDb initializes the PostgreSQL cluster.</param>
    /// <returns>An implementation of <see cref="IPgInitializationSource"/> that will initialize the cluster using InitDb.</returns>
    public IPgInitializationSource InitializeUsingInitDb(PgInitDbOptions options = null)
        => new PgInitDbInitializationSource(environment, options ?? PgInitDbOptions.Default);

    /// <summary>
    /// Creates a new initialization source for setting up the PostgreSQL data cluster using InitDb
    /// with options configured by the specified <paramref name="configurer"/> action.
    /// </summary>
    /// <param name="configurer">A delegate that configures <see cref="PgInitDbOptions"/> to customize InitDb initialization.</param>
    /// <returns>An implementation of <see cref="IPgInitializationSource"/> that will initialize the cluster using InitDb.</returns>
    public IPgInitializationSource InitializeUsingInitDb(Action<PgInitDbOptions> configurer)
    {
        var options = PgInitDbOptions.Default;
        configurer(options);
        return new PgInitDbInitializationSource(environment, options);
    }

    /// <summary>
    /// Creates a new initialization source for restoring a PostgreSQL data cluster from an archive with the provided options.
    /// </summary>
    /// <param name="options">The options that configure how the restoration from the archive will be carried out.</param>
    /// <returns>An implementation of <see cref="IPgInitializationSource"/> that will restore the cluster from the provided archive.</returns>
    public IPgInitializationSource RestoreFromArchive(PgRestoreArchiveOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.ArchiveFilePath, nameof(options.ArchiveFilePath));
        return new PgRestoreArchiveInitializationSource(environment, options);
    }

    /// <summary>
    /// Creates a new initialization source for restoring a PostgreSQL data cluster from an archive
    /// with options configured by the specified <paramref name="configurer"/> action.
    /// </summary>
    /// <param name="configurer">A delegate that configures <see cref="PgRestoreArchiveOptions"/> to customize archive restoration.</param>
    /// <returns>An implementation of <see cref="IPgInitializationSource"/> that will restore the cluster from the provided archive.</returns>
    public IPgInitializationSource RestoreFromArchive(Action<PgRestoreArchiveOptions> configurer)
    {
        PgRestoreArchiveOptions options = new();
        configurer(options);
        return RestoreFromArchive(options);
    }

    /// <summary>
    /// Creates a new instance of <see cref="PgInitializationSourceFactory"/> for the given environment configuration.
    /// </summary>
    /// <param name="environment">The environment configuration for PostgreSQL cluster initialization.</param>
    /// <returns>An instance of <see cref="PgInitializationSourceFactory"/> for the provided environment.</returns>
    public static PgInitializationSourceFactory FromEnvironment(PgEnvironment environment)
        => new PgInitializationSourceFactory(environment);
}
