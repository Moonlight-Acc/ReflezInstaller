using Spectre.Console;

namespace Installer.Helpers;

public class PortHelper
{
    public static async Task<int> Ask(string text, int defaultPort)
    {
        while (true)
        {
            var port = AnsiConsole.Ask($"[bold white]{text}[/]", defaultPort);

            if (await BashHelper.ExecuteCommand($"ss -ltn | grep \":{port} \" | awk '{{print $4}}' | head -n 1") == "")
            {
                ConsoleHelper.Checked($"Port {port} is unused");

                return port;
            }

            ConsoleHelper.Error($"The port {port} is currently in use. Remove the program using this port or choose another port");
        }
    }
}