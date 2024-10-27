using System;

namespace EmbeddedPostgres.Core;

public class PgValidationException(string message, Exception innerException = null)
    : PgCoreException(message, innerException)
{
    public PgValidationException(string message) : this(message, null) { }
}
