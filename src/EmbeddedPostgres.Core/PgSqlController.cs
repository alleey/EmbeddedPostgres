using EmbeddedPostgres.Core;
using EmbeddedPostgres.Core.Extensions;
using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TinyCsvParser;
using TinyCsvParser.Mapping;

namespace EmbeddedPostgres;

internal class PgSqlController : IPgSqlController
{
    private readonly PgInstanceConfiguration instance;
    private readonly string psqlPath;
    private readonly IFileSystem fileSystem;
    private readonly ICommandExecutor commandExecutor;

    public PgSqlController(
        string psqlPathOrFilename,
        PgInstanceConfiguration instance,
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor)
    {
        this.instance = instance;
        this.fileSystem = fileSystem;
        this.commandExecutor = commandExecutor;
        // An absolute path is used if provided
        this.psqlPath = Path.Combine(Path.GetFullPath(Path.Combine(instance.InstanceDirectory, "bin")), psqlPathOrFilename);
    }

    public PgInstanceConfiguration Instance => instance;

    /// <summary>
    /// Retrieves the psql version asynchronously.
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
        => this.GetVersionAsync(psqlPath, commandExecutor, noThrow, cancellationToken);

    class PgDatabaseInfoMapping : CsvMapping<PgDatabaseInfo>
    {
        public PgDatabaseInfoMapping()
        {
            MapProperty(0, x => x.Name);
            MapProperty(1, x => x.Owner);
            MapProperty(2, x => x.Encoding);
            MapProperty(3, x => x.LocaleProvider);
            MapProperty(4, x => x.Collate);
            MapProperty(5, x => x.Ctype);
            MapProperty(6, x => x.Locale);
            MapProperty(7, x => x.ICURules);
            MapProperty(8, x => x.AccessPrivileges);
        }
    }

    /// <summary>
    /// Lists the PostgreSQL databases asynchronously.
    /// </summary>
    /// <param name="listener">
    /// A callback function invoked for each database in the result set. 
    /// It takes a <see cref="PgDatabaseInfo"/> object representing the database information and a <see cref="CancellationToken"/> for task cancellation.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the task if needed.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation of listing the databases.
    /// </returns>
    /// <exception cref="PgCoreException">
    /// Thrown when an error occurs during the execution of the PostgreSQL command.
    /// </exception>
    public async Task ListDatabasesAsync(PgDataClusterConfiguration dataCluster, Func<PgDatabaseInfo, CancellationToken, Task> listener, CancellationToken cancellationToken = default)
    {
        var args = BuildArguments();

        try
        {
            var csvParserOptions = new CsvParserOptions(skipHeader: false, fieldsSeparator: ',');
            var csvParser = new CsvParser<PgDatabaseInfo>(csvParserOptions, new PgDatabaseInfoMapping());
            var csvReaderOptions = new CsvReaderOptions(new[] { "\n" });

            var result = await commandExecutor.ExecuteAsync(
                psqlPath,
                args,
                outputListener: async (line, ct) =>
                {

                    var record = csvParser.ReadFromString(csvReaderOptions, line).FirstOrDefault();
                    if (record.IsValid)
                    {
                        await listener(record.Result, ct).ConfigureAwait(false);
                    }
                },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{psqlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        string[] BuildArguments()
        {
            string[] args = [
                "-U", dataCluster.Superuser,
                "-h", dataCluster.Host,
                "-p", $"{dataCluster.Port}",
                "--list", 
                "--csv", 
                "--tuples-only"
            ];

            return args;
        }
    }

    /// <summary>
    /// Executes a SQL command asynchronously against the specified database. 
    /// The execution can be customized by providing a database name, user name, result format, and an optional listener for processing output.
    /// </summary>
    /// <param name="sql">The SQL command to be executed.</param>
    /// <param name="databaseName">The name of the target database. If null, the default database is used.</param>
    /// <param name="userName">The user name to use for the database connection. If null, the default user is used.</param>
    /// <param name="listener">
    /// An optional listener function that is called during the file execution, allowing real-time processing of the output. 
    /// The function takes a string representing output and a <see cref="CancellationToken"/> for managing task cancellation.
    /// </param>
    /// <param name="format">
    /// Specifies the format of the result set, such as text or binary. The default is <see cref="PgSqlResultFormat"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled if necessary.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous execution operation. The task completes when the file execution finishes.
    /// </returns>
    public Task ExecuteSqlAsync(
        PgDataClusterConfiguration dataCluster,
        string sql,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default) => ExecuteFileAsyncInternal(
            dataCluster,
            sql,
            false,
            databaseName,
            userName,
            listener,
            format,
            cancellationToken);

    /// <summary>
    /// Executes a SQL file asynchronously against the specified database. 
    /// The execution can be customized by providing a database name, user name, result format, and an optional listener for processing output.
    /// </summary>
    /// <param name="filePath">The path to the SQL file that should be executed.</param>
    /// <param name="databaseName">The name of the target database. If null, the default database is used.</param>
    /// <param name="userName">The user name to use for the database connection. If null, the default user is used.</param>
    /// <param name="listener">
    /// An optional listener function that is called during the file execution, allowing real-time processing of the output. 
    /// The function takes a string representing output and a <see cref="CancellationToken"/> for managing task cancellation.
    /// </param>
    /// <param name="format">
    /// Specifies the format of the result set, such as text or binary. The default is <see cref="PgSqlResultFormat"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that allows the task to be canceled if necessary.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous execution operation. The task completes when the file execution finishes.
    /// </returns>
    public Task ExecuteFileAsync(
        PgDataClusterConfiguration dataCluster,
        string filePath,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default) => ExecuteFileAsyncInternal(
            dataCluster,
            filePath,
            true,
            databaseName,
            userName,
            listener,
            format,
            cancellationToken);

    /// <summary>
    /// Common handler for <see cref="ExecuteSqlAsync"/> and <see cref="ExecuteFileAsync"/>
    /// </summary>
    private async Task ExecuteFileAsyncInternal(
        PgDataClusterConfiguration dataCluster,
        string sqlOrPath,
        bool isFile,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sqlOrPath, nameof(sqlOrPath));
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments(format ?? PgSqlResultFormat.Default);

        try
        {
            var result = await commandExecutor.ExecuteAsync(
                psqlPath,
                args,
                outputListener: string.IsNullOrEmpty(format.OutputFile) ? listener : null,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }
        catch (PgCommandExecutionException ex)
        {
            throw new PgCoreException($"{psqlPath} {string.Join(' ', args)} returned an error code {ex.ExitCode}");
        }

        List<string> BuildArguments(PgSqlResultFormat fmt)
        {
            List<string> args = [
                "-U", string.IsNullOrEmpty(userName) ? dataCluster.Superuser : userName,
                "-h", dataCluster.Host,
                "-p", $"{dataCluster.Port}",
                fmt.Format switch
                {
                    PgSqlResultFormat.OutputFormat.UnAligned => "--no-align",
                    PgSqlResultFormat.OutputFormat.CSV => "--csv",
                    _ => ""
                },
                "-F", fmt.FieldSeparator,
                "-R", fmt.RecordSeparator
            ];

            if (!string.IsNullOrEmpty(databaseName))
            {
                args.Add("-d");
                args.Add(databaseName);
            }

            if (!fmt.IncludeHeaders)
            {
                args.Add("--tuples-only");
            }

            if (!string.IsNullOrEmpty(fmt.OutputFile))
            {
                args.Add("-o");
                args.Add(fmt.OutputFile);
            }

            args.Add(isFile ? "-f" : "-c");
            args.Add(sqlOrPath);

            return args;
        }
    }
}
