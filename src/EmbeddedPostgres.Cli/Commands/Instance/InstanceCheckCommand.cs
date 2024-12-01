using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Cli.Commands.Instance;

[Command("instance check")]
public class InstanceCheckCommand : ICommand
{
    private readonly IPgEnvironmentBuilder environmentBuilder;

    public InstanceCheckCommand(IPgEnvironmentBuilder environmentBuilder)
    {
        this.environmentBuilder = environmentBuilder;
    }

    [CommandOption("instance-directory", 'i', Description = "Directory for the PostgreSQL instance.")]
    public string InstanceDirectory { get; init; } = "postgres-test";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            var env = await environmentBuilder.ValidateAsync(InstanceDirectory);
            if (env.Count != 3)
            {
                using (console.WithForegroundColor(ConsoleColor.Yellow))
                {
                    await console.Output.WriteLineAsync($"Could not find a valid Postgres instance in the folder {InstanceDirectory}");
                }
                return;
            }

            using (console.WithForegroundColor(ConsoleColor.Green))
            {
                console.Output.WriteLine($"Found a valid Postgres instance in the folder {InstanceDirectory}");
                foreach (var item in env)
                {
                    await console.Output.WriteLineAsync($"{item.Key} Version: {item.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            await console.Error.WriteLineAsync(ex.Message);
        }

    }
}