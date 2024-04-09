using Spectre.Console;

namespace Installer.Helpers;

public class CommandHelper
{
    public static async Task EnsureCommand(CallContext context, string command, string package, string skipFlag)
    {
        if (await BashHelper.ExecuteCommand($"which {command}", ignoreErrors: true) != "" || context.HasFlag(skipFlag))
            ConsoleHelper.Checked($"{command} already installed");
        else
        {
            await ConsoleHelper.Status($"Installing {command}", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler($"apt install {package} -y", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked($"{command} successfully installed");
        }
    }
}