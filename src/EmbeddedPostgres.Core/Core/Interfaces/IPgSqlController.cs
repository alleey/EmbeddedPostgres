using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Core.Interfaces;

public interface IPgSqlController : IPgExecutableController
{
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
    Task ListDatabasesAsync(
        PgDataClusterConfiguration dataCluster,
        Func<PgDatabaseInfo, CancellationToken, Task> listener,
        CancellationToken cancellationToken = default);

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
    Task ExecuteSqlAsync(
        PgDataClusterConfiguration dataCluster,
        string sql,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default);

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
    Task ExecuteFileAsync(
        PgDataClusterConfiguration dataCluster,
        string filePath,
        string databaseName = null,
        string userName = null,
        Func<string, CancellationToken, Task> listener = default,
        PgSqlResultFormat format = default,
        CancellationToken cancellationToken = default);
}