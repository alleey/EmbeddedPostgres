using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Extensions;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Services;

public class PgEnvironmentBuilderLinux : IPgEnvironmentBuilder
{
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;
    private readonly IFileExtractorFactory fileExtractorFactory;
    private readonly IFileCompressor fileCompressor;
    private readonly Func<string, PgInstanceConfiguration, IPgDataClusterController> dataClusterControllerFactory;
    private readonly Func<string, PgInstanceConfiguration, IPgInitDbController> initDbControllerFactory;
    private readonly Func<string, PgInstanceConfiguration, IPgSqlController> sqlclientFactory;
    private readonly ILogger<PgEnvironmentBuilderLinux> logger;
    private readonly string[] requiredBinaries = [
        "bin/initdb",
        "bin/pg_ctl",
        "bin/postgress"
    ];

    public PgEnvironmentBuilderLinux(
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor,
        IFileExtractorFactory fileExtractorFactory,
        IFileCompressor fileCompressor,
        Func<string, PgInstanceConfiguration, IPgDataClusterController> dataClusterControllerFactory,
        Func<string, PgInstanceConfiguration, IPgInitDbController> initDbControllerFactory,
        Func<string, PgInstanceConfiguration, IPgSqlController> sqlclientFactory,
        ILogger<PgEnvironmentBuilderLinux> logger)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        this.fileExtractorFactory = fileExtractorFactory ?? throw new ArgumentNullException(nameof(fileExtractorFactory));
        this.fileCompressor = fileCompressor ?? throw new ArgumentNullException(nameof(fileCompressor));
        this.dataClusterControllerFactory = dataClusterControllerFactory ?? throw new ArgumentNullException(nameof(dataClusterControllerFactory));
        this.initDbControllerFactory = initDbControllerFactory ?? throw new ArgumentNullException(nameof(initDbControllerFactory));
        this.sqlclientFactory = sqlclientFactory ?? throw new ArgumentNullException(nameof(sqlclientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the specified instance directory to ensure it meets the necessary requirements
    /// for a PostgreSQL instance.
    /// </summary>
    /// <param name="instanceDir">
    /// The directory where the PostgreSQL instance is located. This should be a valid path
    /// that the application can access.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default value
    /// is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous validation operation. The task will complete
    /// once the validation process has finished.
    /// </returns>
    public async Task ValidateAsync(string instanceDir, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation($"Validating environment");

        await requiredBinaries.ParallelForEachAsync(
            async item =>
            {
                try
                {
                    // Ensure the binary exists
                    var binaryPath = Path.GetFullPath(Path.Combine(instanceDir, item));
                    fileSystem.RequireFile(binaryPath);

                    // Run the binary with --version
                    await commandExecutor.ExecuteAsync(
                        binaryPath,
                        ["--version"],
                        outputListener: (line, ct) =>
                        {
                            logger.LogInformation($"{item} version: {line}");
                            return Task.CompletedTask;
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    throw new PgValidationException("PgServer validation failed: " + e.Message);
                }
            },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        logger.LogInformation($"Validation complete!");
    }

    /// <summary>
    /// Builds a <see cref="PgEnvironment"/> based on the specified instance and server configurations.
    /// </summary>
    /// <param name="instanceConfig">
    /// The configuration settings for the PostgreSQL instance.
    /// </param>
    /// <param name="dataClusters">
    /// The configuration settings for the each data cluster in the instance.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the operation. The default value
    /// is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous build operation. The task will contain the 
    /// constructed <see cref="PgEnvironment"/> when completed.
    /// </returns>
    public async Task<PgEnvironment> BuildAsync(
        PgInstanceConfiguration instanceConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalize = instanceConfig.PlatformParameters.GetBoolParameter(PgKnownParameters.NormallizeAttributes, false);
        if (normalize)
        {
            logger.LogInformation($"Normalizing files in directory {instanceConfig.InstanceDirectory}");
            await fileSystem.NormalizeAttributesAsync(instanceConfig.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        }

        var fixPersmissions = instanceConfig.PlatformParameters.GetBoolParameter(PgKnownParameters.Linux.SetExecutableAttributes, false);
        if (fixPersmissions)
        {
            await SetExecutableAttributesAsync(instanceConfig.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        }

        var sqlClient = sqlclientFactory("psql", instanceConfig);
        var sqlClientVer = await sqlClient.GetVersionAsync(noThrow: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PgEnvironment
        {
            Instance = instanceConfig,
            InitDb = initDbControllerFactory("initdb", instanceConfig),
            Controller = dataClusterControllerFactory("pg_ctl", instanceConfig),
            SqlClient = sqlClientVer == null ? null : sqlClient,
            FileSystem = fileSystem,
            CommandExecutor = commandExecutor,
            FileCompressor = fileCompressor,
            FileExtractorFactory = fileExtractorFactory,
        };
    }

    private async Task SetExecutableAttributesAsync(string instanceDir, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (string bin in requiredBinaries)
        {
            var binaryPath = Path.GetFullPath(Path.Combine(instanceDir, bin));
            try
            {
                logger.LogInformation($"Setting executable attribute on {binaryPath}");
                await commandExecutor.ExecuteAsync("chmod", ["+x", binaryPath], cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (PgCommandExecutionException ex)
            {
                throw new PgCoreException($"chmod +x {binaryPath} returned an error code {ex.ExitCode}");
            }
        }
    }
}
