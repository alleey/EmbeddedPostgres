using EmbeddedPostgres.Core;
using System;

namespace EmbeddedPostgres.Infrastructure;

public class PgCommandExecutionException(int exitCode, string message, Exception innerException = null)
    : PgCoreException(message, innerException)
{
    public PgCommandExecutionException(int exitCode, string message) : this(exitCode, message, null) { }

    public int ExitCode { get; } = exitCode;
}
