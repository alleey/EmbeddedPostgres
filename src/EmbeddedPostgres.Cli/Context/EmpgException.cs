namespace EmbeddedPostgres.Cli.Context;

/// <summary>
/// An error with a message intended to be shown to the user as-is, without a stack trace.
/// </summary>
public class EmpgException : Exception
{
    public EmpgException(string message) : base(message)
    {
    }

    public EmpgException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
