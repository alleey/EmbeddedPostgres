using System;
using System.Linq;
using System.Text;

namespace EmbeddedPostgres.Core;

public class PgCoreException : Exception
{
    public PgCoreException()
    {
    }

    public PgCoreException(string message) : base(message)
    {
    }

    public PgCoreException(string message, AggregateException e) : base(BuildMessage(message, e))
    {
    }

    public PgCoreException(string message, Exception innerException) : base(message, innerException)
    {
    }

    private static string BuildMessage(string message, AggregateException e)
    {
        var sb = new StringBuilder(message);
        sb.AppendLine();
        sb.AppendJoin("\n", e.Flatten().InnerExceptions.Select(inner => inner.Message));
        return sb.ToString();
    }
}
