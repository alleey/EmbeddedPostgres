using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Controllers;

internal class PgInitDbController : IPgInitDbController
{
    private readonly PgInstanceConfiguration instance;
    private readonly string initDbPath;
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;
    private readonly ILogger<IPgInitDbController> logger;

    public PgInitDbController(
        string initDbPathOrFilename,
        PgInstanceConfiguration instance,
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor,
        ILogger<IPgInitDbController> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(initDbPathOrFilename, nameof(initDbPathOrFilename));

        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // An absolute path is used if provided
        this.initDbPath = Path.Combine(Path.GetFullPath(Path.Combine(instance.InstanceDirectory, "bin")), initDbPathOrFilename);
    }

    public PgInstanceConfiguration Instance => instance;

    /// <summary>
    /// Retrieves the initdb version asynchronously.
    /// </summary>
    /// <param name="noThrow">
    /// If set to <c>true</c>, the method will not throw an exception if the version retrieval fails; 
    /// instead, it will return <c>null</c>. If set to <c>false</c>, an exception will be thrown on failure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task{String}"/> representing the asynchronous operation. 
    /// The task result contains the PostgreSQL version string, or <c>null</c> if <paramref name="noThrow"/> is <c>true</c> and the operation fails.
    /// </returns>
    public Task<string> GetVersionAsync(bool noThrow = true, CancellationToken cancellationToken = default)
        => this.GetVersionAsync(initDbPath, commandExecutor, noThrow, cancellationToken);

    /// <summary>
    /// Checks whether the PostgreSQL data cluster in the specified data directory has been initialized.
    /// The initialization is determined by the presence of the <c>PG_VERSION</c> file in the data directory.
    /// </summary>
    /// <param name="dataDirectory">
    /// The path to the data directory to check for initialization. 
    /// If null, the default data directory is used.
    /// </param>
    /// <returns>
    /// <c>true</c> if the data cluster is initialized (i.e., the <c>PG_VERSION</c> file exists); otherwise, <c>false</c>.
    /// </returns>
    public bool IsInitialized(PgDataClusterConfiguration dataCluster) 
        => fileSystem.FileExists(Path.Combine(instance.GetDataFullPath(dataCluster), "PG_VERSION"));

    /// <summary>
    /// Initializes a PostgreSQL data cluster in the specified data directory by running the `initdb` command.
    /// If the data cluster is already initialized, the method returns without performing any action.
    /// </summary>
    /// <param name="dataDirectory">
    /// The path to the data directory where the PostgreSQL data cluster will be initialized. 
    /// If null, the default data directory as specified in <see cref="Instance"/> is used.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the initialization process to be canceled.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. The task completes when the data cluster initialization finishes.
    /// </returns>
    /// <exception cref="PgCoreException">
    /// Thrown if an error occurs during the execution of the `initdb` command.
    /// </exception>
    public async Task InitializeAsync(PgDataClusterConfiguration dataCluster, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if already initialized
        if (IsInitialized(dataCluster))
        {
            return;
        }

        List<string> args = [
            "-U", dataCluster.Superuser,
            "-D", instance.GetDataFullPath(dataCluster), 
            "-E", dataCluster.Encoding
        ];

        // add --locale if provided
        if (!string.IsNullOrEmpty(dataCluster.Locale))
        {
            args.Add("--locale");
            args.Add(dataCluster.Locale);
        }

        // add --allow-group-access if provided
        if (dataCluster.AllowGroupAccess ?? false)
        {
            args.Add("--allow-group-access");
        }

        try
        {
            await commandExecutor.ExecuteAsync(initDbPath, args, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException(ex.Message);
        }
    }
}
