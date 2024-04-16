using System.Text;
using Installer.Helpers;
using Installer.Models;
using Newtonsoft.Json;
using Spectre.Console;

namespace Installer.Installers;

public static class PanelInstaller
{
    public static async Task Install()
    {
        var basicConfig = new BasicPanelConfigModel();
        
        bool dockerInstalled = false;

        await DisplayHelper.RunAsStatus("[white]Checking if docker is installed on your system[/]",
            async () => { dockerInstalled = await BashHelper.ExecuteCommandForExitCode("docker") == 0; });

        if (!dockerInstalled)
        {
            AnsiConsole.MarkupLine("[white]Docker seems [red]not[/] to be installed[/]");

            if (AnsiConsole.Confirm("[white]Do you want to install docker?[/]"))
            {
                try
                {
                    await DisplayHelper.RunAsStatus("[white]Installing docker[/]",
                        async () =>
                        {
                            await BashHelper.ExecuteCommand("curl -sSL https://get.docker.com/ | CHANNEL=stable bash");
                        });

                    AnsiConsole.MarkupLine("[white]Docker has been [green]successfully[/] installed on your system[/]");
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLine("[white]An [red]error[/] occured while installing docker[/]");
                    AnsiConsole.WriteLine(e.Message);
                    return;
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[white]Docker is [aqua]installed[/][/]");
        }

        var channel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[white]Please select the release channel you want to use (use beta for latest stable release)[/]")
                .AddChoices(
                    "beta",
                    "canary"
                )
        );

        var moonlightHasBeenInstalled = false;

        await DisplayHelper.RunAsStatus(
            "[white]Checking if reflez has been already installed at some point of time[/]",
            async () =>
            {
                moonlightHasBeenInstalled =
                    await BashHelper.ExecuteCommandForExitCode(
                        "docker images --format \"{{.Repository}}\" | grep -w \"^reflez-dev/panel$\"") == 0;
            }
        );

        if (!Directory.Exists("/var/lib/docker/volumes/reflez"))
        {
            AnsiConsole.MarkupLine("[white]The reflez volume is [red]missing[/]. Creating the required volume[/]");
            await BashHelper.ExecuteCommand("docker volume create reflez");
        }
        else
        {
            AnsiConsole.MarkupLine("[white]The reflez volume is already [green]created[/][/]");
        }

        if (!moonlightHasBeenInstalled)
        {
            AnsiConsole.MarkupLine("[white]It seems that you have never installed reflez before[/]");
            AnsiConsole.MarkupLine("[white]Starting initial setup[/]");

            if (AnsiConsole.Confirm(
                    "[white]Do you want to use a local mysql instance running in a docker container? If you already configured the reflez database, deny this option[/]"))
            {
                var password = GenerateString(32);
                var command = $"docker run -d --restart=always --add-host=host.docker.internal:host-gateway --publish 0.0.0.0:3307:3306 --name mlmysql -v mlmysql:/var/lib/mysql -e MYSQL_ROOT_PASSWORD={password} -e MYSQL_DATABASE=reflez -e MYSQL_USER=reflez -e MYSQL_PASSWORD={password} mysql:latest";

                await DisplayHelper.RunAsStatus("[white]Creating mysql container[/]", async () =>
                {
                    await BashHelper.ExecuteCommand(command, showOutput: true);
                });

                basicConfig.Moonlight.Database.Host = "host.docker.internal";
                basicConfig.Moonlight.Database.Port = 3307;
                basicConfig.Moonlight.Database.Username = "reflez";
                basicConfig.Moonlight.Database.Password = password;
                basicConfig.Moonlight.Database.Database = "reflez";
            }
            else if(AnsiConsole.Confirm("[white]Do you want to configure an external database? If you already configured the reflez database, deny this option[/]"))
            {
                basicConfig.Moonlight.Database.Host = AnsiConsole.Ask<string>("[white]Enter the database host (not localhost or 127.0.0.1)[/]");
                basicConfig.Moonlight.Database.Port = AnsiConsole.Ask<int>("[white]Enter the database port[/]");
                basicConfig.Moonlight.Database.Username = AnsiConsole.Ask<string>("[white]Enter the database username[/]");
                basicConfig.Moonlight.Database.Password = AnsiConsole.Ask<string>("[white]Enter the database password[/]");
                basicConfig.Moonlight.Database.Database = AnsiConsole.Ask<string>("[white]Enter the database name[/]");
            }

            string defaultIp = "your-moonlight-domain.de";

            try
            {
                using var httpClient = new HttpClient();
                defaultIp = await httpClient.GetStringAsync("https://api.ipify.org");
            }
            catch (Exception) { /* ignored */ }

            AnsiConsole.WriteLine();
            
            do
            {
                basicConfig.Moonlight.AppUrl = AnsiConsole.Ask<string>("[white]Enter the app url for moonlight (with http or https)[/]", $"http://{defaultIp}");
            } 
            while (
                string.IsNullOrEmpty(basicConfig.Moonlight.AppUrl) ||
                   !basicConfig.Moonlight.AppUrl.StartsWith("http")
            );

            AnsiConsole.MarkupLine("[white]Saving config file...[/]");
            
            Directory.CreateDirectory("/var/lib/docker/volumes/moonlight/_data/configs/");
            await File.WriteAllTextAsync("/var/lib/docker/volumes/moonlight/_data/configs/config.json", JsonConvert.SerializeObject(basicConfig));
        }
        else
            AnsiConsole.MarkupLine("[white]It seems you had already reflez installed, so we are skipping the configuration steps for you[/]");

        var moonlightContainerExisting = false;

        await DisplayHelper.RunAsStatus("[white]Checking for existing reflez container[/]", async () =>
        {
            moonlightContainerExisting =
                !string.IsNullOrEmpty(await BashHelper.ExecuteCommand("docker ps -q -f name=reflez"));
        });

        if (moonlightContainerExisting)
        {
            var moonlightContainerExited = false;
            
            await DisplayHelper.RunAsStatus("[white]Checking for the status of the existing reflez container[/]", async () =>
            {
                moonlightContainerExited =
                    !string.IsNullOrEmpty(await BashHelper.ExecuteCommand("docker ps -aq -f status=exited -f name=reflez"));
            });

            if (!moonlightContainerExited)
            {
                await DisplayHelper.RunAsStatus("[white]Stopping reflez container[/]", async () =>
                {
                    await BashHelper.ExecuteCommand("docker kill reflez");
                });
            }
            
            await DisplayHelper.RunAsStatus("[white]Removing reflez container[/]", async () =>
            {
                await BashHelper.ExecuteCommand("docker rm reflez");
            });
        }
        
        await DisplayHelper.RunAsStatus("[white]Removing old reflez images if existing[/]", async () =>
        {
            await BashHelper.ExecuteCommand($"docker image rm reflez-dev/panel:{channel}", true);
        });
        
        await DisplayHelper.RunAsStatus("[white]Pulling reflez docker image[/]", async () =>
        {
            await BashHelper.ExecuteCommand($"docker pull reflez-dev/panel:{channel}");
        });
        
        await DisplayHelper.RunAsStatus("[white]Creating reflez container[/]", async () =>
        {
            await BashHelper.ExecuteCommand($"docker run -d -p 80:80 -p 443:443 --add-host=host.docker.internal:host-gateway -v moonlight:/app/storage --name reflez --restart=always reflez-dev/panel:{channel}");
        });
    }

    private static string GenerateString(int length)
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var stringBuilder = new StringBuilder();
        var random = new Random();

        for (int i = 0; i < length; i++)
        {
            stringBuilder.Append(chars[random.Next(chars.Length)]);
        }

        return stringBuilder.ToString();
    }
}
