using System.Text.RegularExpressions;
using Installer.Abstractions;
using Installer.DependencyImplementations;
using Installer.Helpers;
using Installer.Models;
using MoonCore.Services;
using Newtonsoft.Json;
using Spectre.Console;

namespace Installer.SoftwareImplementations;

public class DaemonSoftware : ISoftware
{
    public string Id => "daemon";
    public string Name => "Moonlight Daemon";

    private readonly string BinaryPath = "/usr/local/bin/MoonlightDaemon";
    
    public IDependency[] Dependencies { get; } =
    {
        new DockerDependency()
    };

    public Task<bool> CheckFulfilled(CallContext context)
    {
        return Task.FromResult(File.Exists(BinaryPath));
    }

    public async Task Install(CallContext context)
    {
        ConsoleHelper.Info("Disabling systemd service if existent");
        await BashHelper.ExecuteCommand("systemctl disable --now mldaemon", ignoreErrors: true);

        string channel;

        if (!context.HasParameter("--use-channel"))
        {
            channel = ConsoleHelper.Selection("Select the build channel you want to use:", new[]
            {
                "custom",
                "release"
            });
        }
        else
            channel = context.GetRequiredParameterValue<string>("--use-channel");

        // TODO: Remove when release if available
        if (channel == "release")
        {
            ConsoleHelper.Error("Channel is not implemented yet");
            Environment.Exit(0);
        }

        if (channel == "custom")
            await BuildCustom(context);

        await BashHelper.ExecuteCommand("chmod +x " + BinaryPath);
        ConsoleHelper.Checked("Marked daemon binary as an executable");

        bool enableSsl;

        if (context.HasParameter("--use-ssl"))
            enableSsl = context.GetRequiredParameterValue<bool>("--use-ssl");
        else
            enableSsl = AnsiConsole.Confirm(
                "[white bold]Do you want to use ssl for https connections to the daemon?[/]", false);

        string fqdn;

        if (context.HasParameter("--use-fqdn"))
            fqdn = context.GetRequiredParameterValue<string>("--use-fqdn");
        else
            fqdn = AnsiConsole.Ask<string>("[white bold]Enter the fqdn of the node:[/]");

        if (!File.Exists("/etc/moonlight/config.json") && !context.HasFlag("--skip-config"))
        {
            Directory.CreateDirectory("/etc/moonlight");
            
            var configService = new ConfigService<ConfigV1>("/etc/moonlight/config.json");
            var config = configService.Get();
            
            config.Docker.DnsServers.Add("1.1.1.1");
            config.Docker.DnsServers.Add("9.9.9.9");

            if (context.HasParameter("--use-ftp-port"))
                config.Ftp.Port = context.GetRequiredParameterValue<int>("--use-ftp-port");
            else
                config.Ftp.Port = await PortHelper.Ask("Please enter the ftp port the daemon should listen on", 2021);

            if (context.HasParameter("--use-http-port"))
                config.Http.HttpPort = context.GetRequiredParameterValue<int>("--use-http-port");
            else
                config.Http.HttpPort =
                    await PortHelper.Ask("Please enter the http port the daemon should listen on", 8080);

            if (context.HasParameter("--use-remote-url"))
                config.Remote.Url = context.GetRequiredParameterValue<string>("--use-remote-url");
            else
                config.Remote.Url = AnsiConsole.Ask<string>("[white bold]Enter the url of your moonlight instance:[/]");

            if (context.HasParameter("--use-remote-token"))
                config.Remote.Token = context.GetRequiredParameterValue<string>("--use-remote-token");
            else
                config.Remote.Token =
                    AnsiConsole.Prompt(new TextPrompt<string>("[white bold]Enter the token of your node[/]").Secret());

            config.Http.UseSsl = enableSsl;
            config.Http.Fqdn = fqdn;

            configService.Save();

            ConsoleHelper.Checked("Written configuration to disk");
        }

        if (enableSsl && !context.HasFlag("--skip-lets-encrypt"))
        {
            await CommandHelper.EnsureCommand(context, "certbot", "python3-certbot", "--skip-certbot");

            while (true)
            {
                if (await BashHelper.ExecuteCommand("ss -ltn | grep \":80\" | awk '{print $4}' | head -n 1") ==
                    "")
                {
                    ConsoleHelper.Checked("Port 80 is ready to be used by certbot");
                    break;
                }

                ConsoleHelper.Error(
                    "The port 80 is currently in use. In order to start the certbot, port 80 needs to be available. Please stop any program running on port 80 and press [Enter]");
                Console.ReadLine();
            }

            string email;

            if (context.HasParameter("--use-email"))
                email = context.GetRequiredParameterValue<string>("--use-email");
            else
            {
                while (true)
                {
                    var input = AnsiConsole.Ask<string>(
                        "[bold white]Please enter the email address you want to use for lets encrypt expire notices:[/]");

                    if (Regex.IsMatch(input, "^.+@.+$"))
                    {
                        email = input;
                        break;
                    }

                    ConsoleHelper.Error("Please enter a valid email address");
                }
            }

            await ConsoleHelper.Status("Loading lets encrypt certificate", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler(
                    $"certbot certonly --standalone --non-interactive --agree-tos --email {email} -d {fqdn}", (s, b) =>
                    {
                        AnsiConsole.WriteLine(s);
                        return Task.CompletedTask;
                    });
            });

            ConsoleHelper.Checked("Successfully issued certificate");
        }

        Directory.CreateDirectory("/var/lib/moonlight");
        ConsoleHelper.Checked("Created working directory");

        if (!context.HasFlag("--skip-systemd"))
        {
            var content =
                "[Unit]\n" +
                "Description=MoonlightDaemon\n" +
                "Wants=network.target\n" +
                "After=network.target\n" +
                "\n" +
                "[Service]\n" +
                "User=root\n" +
                "WorkingDirectory=/var/lib/moonlight/\n" +
                $"ExecStart=/bin/sh -c \"{BinaryPath} >> /var/log/moonlight.log\"" +
                "\n" +
                "\n" +
                "[Install]\n" +
                "WantedBy=multi-user.target";

            await File.WriteAllTextAsync("/etc/systemd/system/mldaemon.service", content);
            ConsoleHelper.Checked("Created/updated systemd service file");

            await ConsoleHelper.Status("Processing systemd", async action =>
            {
                action.Invoke("Reloading systemd daemon");
                await BashHelper.ExecuteCommand("systemctl daemon-reload");
                
                action.Invoke("Enabling and starting mldaemon service");
                await BashHelper.ExecuteCommand("systemctl enable --now mldaemon");
            });
        }
        
        var flagsForUpdate = new List<string>();
        
        flagsForUpdate.Add("--use-channel");
        flagsForUpdate.Add(channel);
        
        flagsForUpdate.Add("--use-ssl");
        flagsForUpdate.Add(enableSsl.ToString().ToLower());
        
        flagsForUpdate.Add("--skip-lets-encrypt");
        
        flagsForUpdate.Add("--use-fqdn");
        flagsForUpdate.Add(fqdn);
        
        

        ConsoleHelper.Info("Writing update flags to disk");
        Directory.CreateDirectory("/etc/moonlight");
        await File.WriteAllTextAsync("/etc/moonlight/update-daemon.flags", string.Join(' ', flagsForUpdate));
        
        ConsoleHelper.Info("Done! Moonlight Daemon is successfully installed and should be shown as online in the panel every second. If not, refresh the page and check the logs for errors");
        
        AnsiConsole.WriteLine();
        ConsoleHelper.Info("Some useful commands:");
        ConsoleHelper.Info("mlcli daemon restart\tRestart the moonlight daemon");
        ConsoleHelper.Info("mlcli daemon logs\tShows the logs of moonlight");
        ConsoleHelper.Info("mlcli daemon config\tOpens the config file of the daemon in a editor");
    }

    public async Task BuildCustom(CallContext context)
    {
        var arch = context.Storage.Get<string>("ARCH");

        await ConsoleHelper.Status("Installing dotnet sdk 7.0", async action =>
        {
            await BashHelper.ExecuteWithOutputHandler("apt install dotnet-sdk-7.0 -y", (s, b) =>
            {
                AnsiConsole.WriteLine(s);
                return Task.CompletedTask;
            });
        });

        ConsoleHelper.Checked("Successfully installed dotnet sdk 7");

        await CommandHelper.EnsureCommand(context, "git", "git", "--skip-git");

        // Build dir
        if (Directory.Exists("/tmp/mlbuild") && !context.HasFlag("--skip-delete-ml-build"))
            Directory.Delete("/tmp/mlbuild", true);

        Directory.CreateDirectory("/tmp/mlbuild");

        ConsoleHelper.Info("Created tmp build directories");

        if (context.HasFlag("--git-pull"))
        {
            // Git pull
            await ConsoleHelper.Status("Pulling changes", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler("git pull", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });

            ConsoleHelper.Checked("Successfully pulled changes");
        }
        else
        {
            // Git clone
            await ConsoleHelper.Status("Cloning repository", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler(
                    "git clone https://github.com/Moonlight-Panel/MoonlightDaemon /tmp/mlbuild --branch main", (s, b) =>
                    {
                        AnsiConsole.WriteLine(s);
                        return Task.CompletedTask;
                    });
            });

            ConsoleHelper.Checked("Successfully cloned repository");
        }

        await ConsoleHelper.Status($"Compiling for {arch}", async action =>
        {
            await BashHelper.ExecuteWithOutputHandler(
                $"dotnet publish -c Release -r linux-{arch} --self-contained true /p:PublishSingleFile=true", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                }, workingDir: "/tmp/mlbuild/");
        });

        ConsoleHelper.Checked("Compiled daemon from source");

        if (File.Exists(BinaryPath))
        {
            File.Delete(BinaryPath);
            ConsoleHelper.Checked("Removed old binary");
        }
        
        File.Copy($"/tmp/mlbuild/MoonlightDaemon/bin/Release/net7.0/linux-{arch}/publish/MoonlightDaemon",
            BinaryPath);
        ConsoleHelper.Checked("Copied daemon binary");
    }

    public async Task Update(CallContext context)
    {
        if (!File.Exists("/etc/moonlight/update.flags"))
        {
            ConsoleHelper.Error("Unable to find the update flags. Please run the install command to configure your moonlight daemon installation again");
            return;
        }

        var flags = await File.ReadAllTextAsync("/etc/moonlight/update-daemon.flags");
        context.Args.AddRange(flags.Split(" ").Where(x => !string.IsNullOrEmpty(x)));

        await Install(context);
    }

    public async Task Uninstall(CallContext context)
    {
        await ConsoleHelper.Status("Removing moonlight daemon", async action =>
        {
            await BashHelper.ExecuteCommand("systemctl disable --now mldaemon", ignoreErrors: true);
            
            if(File.Exists("/etc/systemd/system/mldaemon.service"))
                File.Delete("/etc/systemd/system/mldaemon.service");
            
            if(File.Exists("/usr/local/bin/MoonlightDaemon"))
                File.Delete("/usr/local/bin/MoonlightDaemon");
            
            if(File.Exists("/etc/moonlight/config.json.bak"))
                File.Delete("/etc/moonlight/config.json.bak");
            
            if(File.Exists("/etc/moonlight/config.json"))
                File.Move("/etc/moonlight/config.json", "/etc/moonlight/config.json.bak");
        });

        ConsoleHelper.Checked("Successfully removed the moonlight daemon");
    }
}