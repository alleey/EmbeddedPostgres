using System.Threading.Tasks;
using System.Threading;

namespace EmbeddedPostgres.Core.Interfaces;

/// <summary>
/// Base interface for all PgSql controllers
/// </summary>
public interface IPgController
{
    PgInstanceConfiguration Instance { get; }
}

/// <summary>
/// </summary>
public interface IPgExecutableController : IPgController
{
    /// <summary>
    /// Retrieves the binary version asynchronously.
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
    Task<string> GetVersionAsync(bool noThrow = true, CancellationToken cancellationToken = default);
}
