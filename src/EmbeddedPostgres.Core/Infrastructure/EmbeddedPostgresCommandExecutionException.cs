using EmbeddedPostgres.Core;
using System;

namespace EmbeddedPostgres.Infrastructure;

public class EmbeddedPostgresCommandExecutionException(int exitCode, string message, Exception innerException = null) 
    : PgCoreException(message, innerException)
{
    public EmbeddedPostgresCommandExecutionException(int exitCode, string message) : this(exitCode, message, null) { }

    public int ExitCode { get; } = exitCode;
}
