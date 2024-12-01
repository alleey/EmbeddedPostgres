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

namespace EmbeddedPostgres.Core.Services;

internal class PgEnvironmentBuilderWindows : IPgEnvironmentBuilder
{
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;
    private readonly IFileExtractorFactory fileExtractorFactory;
    private readonly IFileCompressor fileCompressor;
    private readonly IHttpService httpService;
    private readonly PgControllerFactory controllerFactory;
    private readonly ILogger<PgEnvironmentBuilderWindows> logger;
    private readonly string[] requiredBinaries = {
        "bin/pg_ctl.exe",
        "bin/initdb.exe",
        "bin/postgres.exe"
    };

    public PgEnvironmentBuilderWindows(
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor,
        IFileExtractorFactory fileExtractorFactory,
        IFileCompressor fileCompressor,
        IHttpService httpService,
        PgControllerFactory controllerFactory,
        ILogger<PgEnvironmentBuilderWindows> logger)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        this.fileExtractorFactory = fileExtractorFactory ?? throw new ArgumentNullException(nameof(fileExtractorFactory));
        this.fileCompressor = fileCompressor ?? throw new ArgumentNullException(nameof(fileCompressor));
        this.httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        this.controllerFactory = controllerFactory ?? throw new ArgumentNullException(nameof(controllerFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the specified instance directory to ensure it meets the necessary requirements
    /// for a PostgreSQL instance. This method checks the existence of required binaries and
    /// retrieves their versions, returning a dictionary with binary names as keys and version
    /// information as values.
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
    /// A task that represents the asynchronous validation operation, containing a dictionary
    /// where keys are binary names and values are the respective version information.
    /// </returns>
    /// <exception cref="PgValidationException">
    /// Thrown if validation fails due to a missing binary or an error during version retrieval.
    /// </exception>
    public async Task<Dictionary<string, string>> ValidateAsync(string instanceDir, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Validating environment");

        // Dictionary to store binary versions
        var versions = new Dictionary<string, string>();

        // Iterate over required binaries and capture versions
        await requiredBinaries.ParallelForEachAsync(
            async item =>
            {
                try
                {
                    // Ensure the binary exists
                    var binaryPath = Path.GetFullPath(Path.Combine(instanceDir, item));
                    fileSystem.RequireFile(binaryPath);

                    // Temporary variable to hold the version output
                    string versionOutput = null;

                    // Run the binary with --version and capture the output
                    await commandExecutor.ExecuteAsync(
                        binaryPath,
                        ["--version"],
                        outputListener: (line, ct) =>
                        {
                            versionOutput = line;
                            return Task.CompletedTask;
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Store the version in the dictionary
                    if (versionOutput != null)
                    {
                        versions[Path.GetFileNameWithoutExtension(item)] = versionOutput;
                        logger.LogInformation($"{item} version: {versionOutput}");
                    }
                }
                catch (Exception e)
                {
                    throw new PgValidationException("PgServer validation failed: " + e.Message);
                }
            },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        logger.LogInformation("Validation complete!");

        return versions;
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

        var fixPersmissions = instanceConfig.PlatformParameters.GetBoolParameter(PgKnownParameters.Windows.AddLocalUserAccessPermission, false);
        if (fixPersmissions)
        {
            logger.LogInformation($"Adding local user access permissions on {instanceConfig.InstanceDirectory}");
            await AddLocalUserAccessPermissionAsync(instanceConfig.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        }

        return new PgEnvironment
        {
            Instance = instanceConfig,
            InitDbController = controllerFactory.GetController<IPgInitDbController>("initdb.exe", instanceConfig),
            DataClusterController = controllerFactory.GetController<IPgDataClusterController>("pg_ctl.exe", instanceConfig),

            SqlController = await GetOptionalControllerAsync<IPgSqlController>("psql.exe", instanceConfig, cancellationToken).ConfigureAwait(false),
            RestoreController = await GetOptionalControllerAsync<IPgRestoreController>("pg_restore.exe", instanceConfig, cancellationToken).ConfigureAwait(false),
            DumpController = await GetOptionalControllerAsync<IPgDumpController>("pg_dump.exe", instanceConfig, cancellationToken).ConfigureAwait(false),

            FileSystem = fileSystem,
            CommandExecutor = commandExecutor,
            FileCompressor = fileCompressor,
            FileExtractorFactory = fileExtractorFactory,
            HttpService = httpService,
        };

        async Task<T> GetOptionalControllerAsync<T>(string binaryPath, PgInstanceConfiguration instanceConfig, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                var controller = controllerFactory.GetController<T>(binaryPath, instanceConfig);
                var version = await (controller as IPgExecutableController).GetVersionAsync(noThrow: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                return version == null ? null : controller;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
            return null;
        }
    }

    // In some cases like CI environments, local user account will have write access
    // on the Instance directory (Postgres expects write access on the parent of data directory)
    // Otherwise when running initdb, it results in 'initdb: could not change permissions of directory'
    // Also note that the local account should have admin rights to change folder permissions
    private async Task AddLocalUserAccessPermissionAsync(string instanceDir, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sentinel = Path.Combine(instanceDir, "permissions.sentinel");
        // Do this only once
        if (fileSystem.Touch(sentinel))
        {
            // get the local user under which the program runs
            var currentLocalUser = Environment.GetEnvironmentVariable("Username");
            string[] args = [instanceDir, "/t", "/grant:r", $"{currentLocalUser}:(OI)(CI)F"];

            try
            {
                var result = await commandExecutor.ExecuteAsync(
                    "icacls.exe",
                    args,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (PgCommandExecutionException ex)
            {
                fileSystem.DeleteFile(sentinel);
                throw new PgCoreException($"AddLocalUserAccessPermissionAsync: icacls.exe {string.Join(' ', args)} returned an error code {ex.ExitCode}");
            }
        }
    }
}
