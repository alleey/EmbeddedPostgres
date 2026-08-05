using CliFx.Infrastructure;
using EmbeddedPostgres.Cli.Context;

namespace EmbeddedPostgres.Cli.Commands.Role;

/// <summary>
/// Reads role passwords from standard input.
/// </summary>
/// <remarks>
/// There is deliberately no <c>--password</c> option. An argument is visible to every user on the
/// machine through the process list and is normally recorded in shell history, so the only way in
/// is a stream the caller controls:
/// <code>
/// printf '%s' "$SECRET" | empg role password dba --password-stdin
/// </code>
/// </remarks>
public static class RolePassword
{
    public static async Task<string> ReadFromStdinAsync(IConsole console)
    {
        var value = await console.Input.ReadToEndAsync().ConfigureAwait(false);

        // Trailing newlines come from the pipe, not the secret.
        value = value.TrimEnd('\r', '\n');

        if (string.IsNullOrEmpty(value))
        {
            throw new EmpgException("No password was supplied on standard input.");
        }

        return value;
    }
}
