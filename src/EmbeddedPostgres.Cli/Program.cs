using CliFx;
using EmbeddedPostgres.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmbeddedPostgres.Cli;

class Program
{
    /// <summary>
    /// Global options that may be written before the command name, as git allows for
    /// <c>git -C &lt;path&gt; status</c>. Each entry maps the option to whether it takes a value.
    /// </summary>
    private static readonly Dictionary<string, bool> LeadingGlobalOptions = new(StringComparer.Ordinal)
    {
        ["-C"] = true,
        ["--directory"] = true,
        ["-i"] = true,
        ["--instance"] = true,
        ["--json"] = false,
        ["-q"] = false,
        ["--quiet"] = false,
    };

    public static async Task<int> Main(string[] args)
    {
        var builder = new HostBuilder();

        builder
            .ConfigureLogging(logging =>
            {
                // The CLI reports progress through its own output writer; library logging would
                // otherwise interleave with it and break --json.
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((hostContext, services) => ConfigureServices(services));

        var host = builder.Build();

        var cliApp = new CommandLineApplicationBuilder()
            .SetExecutableName("empg")
            .SetTitle("empg")
            .SetDescription("Manage embedded PostgreSQL instances.")
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(host.Services)
            .Build();

        return await cliApp.RunAsync(NormalizeLeadingGlobalOptions(args));
    }

    /// <summary>
    /// Moves global options written before the command name to the end of the argument list.
    /// </summary>
    /// <remarks>
    /// CliFx binds options to the command that precedes them, so <c>empg -C path status</c> would
    /// otherwise be rejected. Rewriting it to <c>empg status -C path</c> keeps the git-style
    /// spelling working without giving up per-command binding.
    /// </remarks>
    internal static IReadOnlyList<string> NormalizeLeadingGlobalOptions(IReadOnlyList<string> args)
    {
        var leading = new List<string>();
        var index = 0;

        while (index < args.Count)
        {
            var token = args[index];

            // `--directory=path` is self-contained and needs no lookahead.
            var separator = token.IndexOf('=');
            if (separator > 0 && LeadingGlobalOptions.ContainsKey(token[..separator]))
            {
                leading.Add(token);
                index++;
                continue;
            }

            if (!LeadingGlobalOptions.TryGetValue(token, out var takesValue))
            {
                break;
            }

            if (takesValue)
            {
                if (index + 1 >= args.Count)
                {
                    // Missing value: leave it alone so CliFx reports the error.
                    break;
                }
                leading.Add(token);
                leading.Add(args[index + 1]);
                index += 2;
            }
            else
            {
                leading.Add(token);
                index++;
            }
        }

        // Nothing to move, or nothing left to attach the options to.
        if (leading.Count == 0 || index >= args.Count)
        {
            return args;
        }

        var normalized = args.Skip(index).ToList();
        normalized.AddRange(leading);
        return normalized;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddEmbeddedPostgresCoreServices();
        services.AddEmbeddedPostgresServices();
        services.AddEmbeddedPostgresCliServices();
    }
}
