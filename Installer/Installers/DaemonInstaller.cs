using Installer.Helpers;
using Spectre.Console;

namespace Installer.Installers;

public static class DaemonInstaller
{
    public static async Task Install()
    {
        AnsiConsole.MarkupLine("[white]Checking for a existing daemon instance[/]");

        if (File.Exists("/etc/systemd/system/reflezdaemon.service"))
        {
            AnsiConsole.MarkupLine("[white]Found existing daemon instance[/]");
            AnsiConsole.MarkupLine("[white][red]Removing[/] existing daemon data[/]");

            await DisplayHelper.RunAsStatus(
                "[white]Stopping systemd service if running[/]",
                async () => { await BashHelper.ExecuteCommand("systemctl stop reflezdaemon"); }
            );

            await DisplayHelper.RunAsStatus(
                "[white]Removing binaries and systemd files[/]",
                () =>
                {
                    try
                    {
                        File.Delete("/lib/reflezdaemon/MoonlightDaemon");
                    }
                    catch (Exception) { /* ignored */ }
                    
                    try
                    {
                        File.Delete("/etc/systemd/system/reflezdaemon.service");
                    }
                    catch (Exception) { /* ignored */ }

                    return Task.CompletedTask;
                }
            );
        }

        AnsiConsole.MarkupLine("[white]Creating environment[/]");
        Directory.CreateDirectory("/lib/reflezdaemon/");

        var architectureType = await BashHelper.ExecuteCommand("uname -m");
        AnsiConsole.MarkupLine($"[white]Selecting architecture {architectureType}[/]");

        await DisplayHelper.RunAsStatus("[white]Downloading daemon binary[/]", async () =>
        {
            using var httpClient = new HttpClient();
            var fs = File.Create("/lib/reflezdaemon/MoonlightDaemon");

            var response = await httpClient
                .GetAsync(
                    $"https://github.com/reflez-dev/ReflezDaemon/releases/download/v1b17/ReflezDaemon_{architectureType}");

            await response.Content.CopyToAsync(fs);

            await fs.FlushAsync();
            fs.Close();
        });

        if (File.Exists("/lib/reflezdaemon/appsettings.json"))
            AnsiConsole.MarkupLine("[white]Found existing config file. Skipped download of the default config file[/]");
        else
        {
            await DisplayHelper.RunAsStatus("[white]Downloading default config file[/]", async () =>
            {
                using var httpClient = new HttpClient();
                var fs = File.Create("/lib/reflezdaemon/appsettings.json");

                var response = await httpClient
                    .GetAsync(
                        "https://install.zentrixcode.com/daemonFiles/appsettings.json");

                await response.Content.CopyToAsync(fs);

                await fs.FlushAsync();
                fs.Close();
            });
        }
        
        await DisplayHelper.RunAsStatus("[white]Downloading systemd service[/]", async () =>
        {
            using var httpClient = new HttpClient();
            var fs = File.Create("/etc/systemd/system/reflezdaemon.service");

            var response = await httpClient
                .GetAsync(
                    "https://install.zentrixcode.com/daemonFiles/reflezdaemon.service");

            await response.Content.CopyToAsync(fs);

            await fs.FlushAsync();
            fs.Close();
        });
        
        await DisplayHelper.RunAsStatus("[white]Changing file permissions[/]", async () =>
        {
            await BashHelper.ExecuteCommand("chmod 664 /etc/systemd/system/reflezdaemon.service");
            await BashHelper.ExecuteCommand("chmod +x /lib/reflezdaemon/ReflezDaemon");
        });
        
        await DisplayHelper.RunAsStatus("[white]Reloading systemd daemon[/]", async () =>
        {
            await BashHelper.ExecuteCommand("systemctl daemon-reload");
        });
        
        await DisplayHelper.RunAsStatus("[white]Enabling reflez daemon[/]", async () =>
        {
            await BashHelper.ExecuteCommand("systemctl enable --now reflezdaemon");
        });
        
        AnsiConsole.MarkupLine("[white]The reflez daemon has been [green]successfully[/] installed[/]");

        if (AnsiConsole.Confirm("[white]Do you want to install wings as well (its required for the daemon to run)?[/]"))
        {
            await WingsInstaller.Install();
        }
    }
}
