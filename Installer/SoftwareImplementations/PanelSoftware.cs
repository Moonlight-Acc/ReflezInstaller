using System.Text.RegularExpressions;
using Installer.Abstractions;
using Installer.DependencyImplementations;
using Installer.Helpers;
using Installer.Models;
using MoonCore.Helpers;
using Newtonsoft.Json;
using Spectre.Console;

namespace Installer.SoftwareImplementations;

public class PanelSoftware : ISoftware
{
    public string Name => "Moonlight Panel";
    public IDependency[] Dependencies { get; } = {
        new DockerDependency()
    };
    
    public async Task<bool> CheckFulfilled(CallContext context)
    {
        return await BashHelper.ExecuteCommandForExitCode("docker container inspect moonlight") == 0;
    }

    public async Task Install(CallContext context)
    {
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

        var dockerImageName = $"moonlightpanel/moonlight:{channel}";

        if (channel == "custom")
            await BuildCustom(context);

        var enableSsl = context.HasParameter("--use-ssl") 
            ? context.GetRequiredParameterValue<bool>("--use-ssl") 
            : AnsiConsole.Confirm("[bold white]Do you want to use ssl (for https connections)[/]", defaultValue: false);
        
        string appHost;

        if (context.HasParameter("--use-app-host"))
            appHost = context.GetRequiredParameterValue<string>("--use-app-host");
        else
        {
            if (enableSsl)
            {
                while (true)
                {
                    var domain = AnsiConsole.Ask<string>("[bold white]Enter the domain you want to use:[/]").Trim();

                    if (Regex.IsMatch(domain, "^(?!-)(?:[a-zA-Z\\d-]{0,62}[a-zA-Z\\d]\\.)+(?:[a-zA-Z]{2,})$"))
                    {
                        appHost = domain;
                        break;
                    }
                    else
                        ConsoleHelper.Error("Please enter a valid domain");
                }
            }
            else
            {
                var defaultIp = "";

                try
                {
                    using var httpClient = new HttpClient();
                    defaultIp = await httpClient.GetStringAsync("https://api.ipify.org");
                }
                catch (Exception e)
                {
                    ConsoleHelper.Error($"An error occured while fetching ipify for the servers ip address: {e.Message}");
                }
            
                while (true)
                {
                    var input = AnsiConsole.Ask("[bold white]Please enter a ip address or domain you want to use:[/]", defaultIp);

                    if (Regex.IsMatch(input, "^(?!-)(?:[a-zA-Z\\d-]{0,62}[a-zA-Z\\d]\\.)+(?:[a-zA-Z]{2,})$"))
                    {
                        appHost = input;
                        break;
                    }
                
                    if (Regex.IsMatch(input, "^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
                    {
                        appHost = input;
                        break;
                    }

                    ConsoleHelper.Error("Please enter a valid ipv4 address or domain");
                }
            }
        }

        var httpPort = 80;
        var httpsPort = 443;

        if (context.HasParameter("--use-http-port"))
            httpPort = context.GetRequiredParameterValue<int>("--use-http-port");
        
        if (context.HasParameter("--use-https-port"))
            httpsPort = context.GetRequiredParameterValue<int>("--use-https-port");

        if (!context.HasParameter("--use-http-port") && !context.HasParameter("--use-https-port"))
        {
            while (true)
            {
                var port = AnsiConsole.Ask("[bold white]Please enter the http port moonlight should listen on[/]", 80);

                if (await BashHelper.ExecuteCommand($"ss -ltn | grep \":{port}\" | awk '{{print $4}}' | head -n 1") == "")
                {
                    ConsoleHelper.Checked($"Port {port} is unused");
                
                    httpPort = port;
                    break;
                }

                ConsoleHelper.Error($"The port {port} is currently in use. Remove the program using this port or choose another port");
            }

            if (enableSsl)
            {
                while (true)
                {
                    var port = AnsiConsole.Ask("[bold white]Please enter the https port moonlight should listen on[/]", 433);

                    if (await BashHelper.ExecuteCommand($"ss -ltn | grep \":{port}\" | awk '{{print $4}}' | head -n 1") == "")
                    {
                        ConsoleHelper.Checked($"Port {port} is unused");
                
                        httpsPort = port;
                        break;
                    }

                    ConsoleHelper.Error($"The port {port} is currently in use. Remove the program using this port or choose another port");
                }
            }
        }

        ConsoleHelper.Info($"App URL: http://{appHost}:{httpPort}");
        
        if(enableSsl)
            ConsoleHelper.Info($"App URL: https://{appHost}:{httpsPort}");

        if (enableSsl && !context.HasFlag("--skip-lets-encrypt"))
        {
            if (await BashHelper.ExecuteCommand("which certbot", ignoreErrors: true) != "" || context.HasFlag("--skip-certbot"))
                ConsoleHelper.Checked("Certbot already installed");
            else
            {
                await ConsoleHelper.Status("Installing Certbot", async action =>
                {
                    await BashHelper.ExecuteWithOutputHandler("apt install python3-certbot -y", (s, b) =>
                    {
                        AnsiConsole.WriteLine(s);
                        return Task.CompletedTask;
                    });
                });
            
                ConsoleHelper.Checked("Certbot successfully installed");
            }

            while (true)
            {
                if (await BashHelper.ExecuteCommand("ss -ltn | grep \":80\" | awk '{print $4}' | head -n 1") ==
                    "")
                {
                    ConsoleHelper.Checked("Port 80 is ready to be used by certbot");
                    break;
                }
                
                ConsoleHelper.Error("The port 80 is currently in use. In order to start the certbot, port 80 needs to be available. Please stop any program running on port 80 and press [Enter]");
                Console.ReadLine();
            }

            string email;

            if (context.HasParameter("--use-email"))
                email = context.GetRequiredParameterValue<string>("--use-email");
            else
            {
                while (true)
                {
                    var input = AnsiConsole.Ask<string>("[bold white]Please enter the email address you want to use for lets encrypt expire notices:[/]");

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
                await BashHelper.ExecuteWithOutputHandler($"certbot certonly --standalone --non-interactive --agree-tos --email {email} -d {appHost}", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("Successfully issued certificate");
        }

        var useInternalDb = context.HasFlag("--use-internal-db") 
            ? true
            : AnsiConsole.Confirm("[white bold]Do you want to use the internal database?[/]");

        string dbHost;
        int dbPort;
        string dbUser;
        string dbPassword;
        string dbDatabase;

        if (useInternalDb)
        {
            dbHost = context.HasParameter("--use-db-host") ? context.GetRequiredParameterValue<string>("--use-db-host") : "host.docker.internal";
            dbPort = context.HasParameter("--use-db-port") ? context.GetRequiredParameterValue<int>("--use-db-port") : 3307;
            dbUser = context.HasParameter("--use-db-user") ? context.GetRequiredParameterValue<string>("--use-db-user") : "moonlight";
            dbPassword = context.HasParameter("--use-db-password") ? context.GetRequiredParameterValue<string>("--use-db-password") : Formatter.GenerateString(32);
            dbDatabase = context.HasParameter("--use-db-database") ? context.GetRequiredParameterValue<string>("--use-db-database") : "moonlight";
        }
        else
        {
            dbHost = context.HasParameter("--use-db-host") ? context.GetRequiredParameterValue<string>("--use-db-host") : AnsiConsole.Ask<string>("[white bold]Please enter the host of your database:[/]");
            dbPort = context.HasParameter("--use-db-port") ? context.GetRequiredParameterValue<int>("--use-db-port") : AnsiConsole.Ask<int>("[white bold]Please enter the port of your database:[/]");
            dbUser = context.HasParameter("--use-db-user") ? context.GetRequiredParameterValue<string>("--use-db-user") : AnsiConsole.Ask<string>("[white bold]Please enter the username of your database:[/]");
            dbPassword = context.HasParameter("--use-db-password") ? context.GetRequiredParameterValue<string>("--use-db-password") : AnsiConsole.Prompt(new TextPrompt<string>("[white bold]Please enter the password of your database:[/]").Secret());
            dbDatabase = context.HasParameter("--use-db-database") ? context.GetRequiredParameterValue<string>("--use-db-database") : AnsiConsole.Ask<string>("[white bold]Please enter the name of your database:[/]");
        }
        
        // UFW
        if(await BashHelper.ExecuteCommand("which ufw") == "" || context.HasFlag("--skip-ufw"))
            ConsoleHelper.Info("UFW not detected. Skipping firewall configurations");
        else if((await BashHelper.ExecuteCommand("ufw status")).Contains("inactive"))
            ConsoleHelper.Info("UFW disabled. Skipping firewall configurations");
        else
        {
            await ConsoleHelper.Status("Adding ufw rules", async action =>
            {
                await BashHelper.ExecuteCommand($"ufw allow {httpPort}", ignoreErrors: true);
                
                if(enableSsl)
                    await BashHelper.ExecuteCommand($"ufw allow {httpsPort}", ignoreErrors: true);
                
                if(useInternalDb)
                    await BashHelper.ExecuteCommand($"ufw allow 3307", ignoreErrors: true);
            });
            
            ConsoleHelper.Checked("UFW configuration updated");
        }

        await ConsoleHelper.Status("Creating volumes", async action =>
        {
            if (await BashHelper.ExecuteCommandForExitCode("docker volume inspect moonlight") == 0)
                ConsoleHelper.Checked("Volume 'moonlight' exists");
            else
            {
                await BashHelper.ExecuteCommand("docker volume create moonlight");
                ConsoleHelper.Checked("Volume 'moonlight' created");
            }

            if (useInternalDb)
            {
                if (await BashHelper.ExecuteCommandForExitCode("docker volume inspect moonlight_db") == 0)
                    ConsoleHelper.Checked("Volume 'moonlight_db' exists");
                else
                {
                    await BashHelper.ExecuteCommand("docker volume create moonlight_db");
                    ConsoleHelper.Checked("Volume 'moonlight_db' created");
                }
            }
        });
        
        ConsoleHelper.Checked("Successfully created volumes");

        // Internal db
        if (useInternalDb)
        {
            await ConsoleHelper.Status("Deploying internal db", async action =>
            {
                action.Invoke("Checking for existing internal database container");
                if (await BashHelper.ExecuteCommandForExitCode("docker container inspect moonlight_db") == 0)
                {
                    action.Invoke("Removing existing internal database container");
                    await BashHelper.ExecuteCommand("docker container kill moonlight_db", ignoreErrors: true);
                    await BashHelper.ExecuteCommand("docker container rm moonlight_db");
                }

                action.Invoke("Creating internal database container");
                
                await BashHelper.ExecuteWithOutputHandler($"docker run -d --restart=always --add-host=host.docker.internal:host-gateway --publish 0.0.0.0:{dbPort}:3306 --name moonlight_db -v moonlight_db:/var/lib/mysql -e MYSQL_ROOT_PASSWORD={dbPassword} -e MYSQL_DATABASE={dbDatabase} -e MYSQL_USER={dbUser} -e MYSQL_PASSWORD={dbPassword} mysql:latest", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("Successfully deployed internal database");
        }

        if (!context.HasFlag("--skip-config"))
        {
            // Core configuration
            var coreConfig = new CoreConfiguration();

            coreConfig.Database.Host = dbHost;
            coreConfig.Database.Port = dbPort;
            coreConfig.Database.Username = dbUser;
            coreConfig.Database.Password = dbPassword;
            coreConfig.Database.Database = dbDatabase;

            coreConfig.Http.HttpPort = 80;
            coreConfig.Http.HttpsPort = 443;

            coreConfig.Http.EnableSsl = enableSsl;
            coreConfig.Http.CertPath = "cert.pem";
            coreConfig.Http.KeyPath = "privkey.pem";

            if(enableSsl)
                coreConfig.AppUrl = $"https://{appHost}:{httpsPort}";
            else
                coreConfig.AppUrl = $"http://{appHost}:{httpPort}";

            Directory.CreateDirectory("/var/lib/docker/volumes/moonlight/_data/configs/");
            await File.WriteAllTextAsync("/var/lib/docker/volumes/moonlight/_data/configs/core.json", JsonConvert.SerializeObject(coreConfig));

            ConsoleHelper.Checked("Core configuration file has been written");
        }
        
        // Deploy
        await ConsoleHelper.Status("Deploying moonlight container", async action =>
        {
            action.Invoke("Checking for existing moonlight container");
            if (await BashHelper.ExecuteCommandForExitCode("docker container inspect moonlight") == 0)
            {
                action.Invoke("Removing existing moonlight container");
                await BashHelper.ExecuteCommand("docker container kill moonlight", ignoreErrors: true);
                await BashHelper.ExecuteCommand("docker container rm moonlight");
            }

            var command =
                $"docker run -d -p {httpPort}:80 --add-host=host.docker.internal:host-gateway -v moonlight:/app/storage --name moonlight --restart=always";

            if (enableSsl)
            {
                command +=
                    $" --mount type=bind,source=/etc/letsencrypt/live/{appHost}/cert.pem,target=/app/cert.pem";

                command +=
                    $" --mount type=bind,source=/etc/letsencrypt/live/{appHost}/privkey.pem,target=/app/privkey.pem";

                command += $" -p {httpsPort}:443";
            }

            command += $" {dockerImageName}";

            action.Invoke("Creating moonlight container");
            await BashHelper.ExecuteWithOutputHandler(command, (s, b) =>
            {
                AnsiConsole.WriteLine(s);
                return Task.CompletedTask;
            });
        });
        
        ConsoleHelper.Checked("Successfully deployed moonlight container");

        var flagsForUpdate = new List<string>();
        
        flagsForUpdate.Add("--use-channel");
        flagsForUpdate.Add(channel);
        
        if(useInternalDb)
            flagsForUpdate.Add("--use-internal-db");
        
        flagsForUpdate.Add("--use-db-host");
        flagsForUpdate.Add(dbHost);
        
        flagsForUpdate.Add("--use-db-port");
        flagsForUpdate.Add(dbPort.ToString());
        
        flagsForUpdate.Add("--use-db-user");
        flagsForUpdate.Add(dbUser);
        
        flagsForUpdate.Add("--use-db-password");
        flagsForUpdate.Add(dbPassword);
        
        flagsForUpdate.Add("--use-db-database");
        flagsForUpdate.Add(dbDatabase);
        
        //flagsForUpdate.Add("--skip-config");
        
        flagsForUpdate.Add("--use-ssl");
        flagsForUpdate.Add(enableSsl.ToString().ToLower());
        
        flagsForUpdate.Add("--skip-lets-encrypt");
        
        flagsForUpdate.Add("--use-app-host");
        flagsForUpdate.Add(appHost);

        flagsForUpdate.Add("--use-http-port");
        flagsForUpdate.Add(httpPort.ToString());
        
        if (enableSsl)
        {
            flagsForUpdate.Add("--use-https-port");
            flagsForUpdate.Add(httpsPort.ToString());
        }

        ConsoleHelper.Info("Writing update flags to disk");
        Directory.CreateDirectory("/etc/moonlight");
        await File.WriteAllTextAsync("/etc/moonlight/update.flags", string.Join(' ', flagsForUpdate));
        
        ConsoleHelper.Info("Done! Moonlight is successfully installed and can be reached under the following urls. Please keep in mind moonlight might take some time to boot up and create the initial data on the first startup");
        AnsiConsole.WriteLine();
        
        ConsoleHelper.Info($"App URL: http://{appHost}:{httpPort}");
        
        if(enableSsl)
            ConsoleHelper.Info($"App URL: https://{appHost}:{httpsPort}");
        
        AnsiConsole.WriteLine();
        ConsoleHelper.Info("Some useful commands:");
        ConsoleHelper.Info("mlcli moonlight login\tSee the default login for moonlight");
        ConsoleHelper.Info("mlcli moonlight logs\tShows the logs of moonlight");
        
        AnsiConsole.WriteLine();
        ConsoleHelper.Info("See https://help.moonlightpanel.xyz/gettingStarted for a guide how to get started and do the initial configuration");
    }

    public async Task BuildCustom(CallContext context)
    {
        if(context.HasFlag("--skip-custom-build"))
            return;
        
        // NodeJS
        if (await BashHelper.ExecuteCommand("which node", ignoreErrors: true) != "" || context.HasFlag("--skip-node-js"))
            ConsoleHelper.Checked("NodeJS already installed");
        else
        {
            await ConsoleHelper.Status("Installing NodeJS", async action =>
            {
                action.Invoke("Setting up repository");
                await BashHelper.ExecuteWithOutputHandler("apt install ca-certificates curl gnupg -y", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });

                await BashHelper.ExecuteCommand("mkdir -p /etc/apt/keyrings");
                await BashHelper.ExecuteCommand(
                    "curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg");

                await BashHelper.ExecuteCommand(
                    "echo \"deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main\" | tee /etc/apt/sources.list.d/nodesource.list");
                
                action.Invoke("Updating package list");
                await BashHelper.ExecuteWithOutputHandler("apt update", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
                
                action.Invoke("Installing packages");
                await BashHelper.ExecuteWithOutputHandler("apt install nodejs -y", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("NodeJS successfully installed");
        }
        
        // SASS
        if(await BashHelper.ExecuteCommand("which sass", ignoreErrors: true) != "" || context.HasFlag("--skip-sass"))
            ConsoleHelper.Checked("Sass already installed");
        else
        {
            await ConsoleHelper.Status("Installing Sass", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler("npm install -g sass", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("Sass successfully installed");
        }
        
        // Git
        if(await BashHelper.ExecuteCommand("which git", ignoreErrors: true) != "" || context.HasFlag("--skip-git"))
            ConsoleHelper.Checked("Git already installed");
        else
        {
            await ConsoleHelper.Status("Installing git", async action =>
            {
                await BashHelper.ExecuteWithOutputHandler("apt install git -y", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("Git successfully installed");
        }
        
        // Build dir
        if(Directory.Exists("/tmp/mlbuild") && !context.HasFlag("--skip-delete-ml-build"))
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
                await BashHelper.ExecuteWithOutputHandler("git clone https://github.com/Moonlight-Panel/Moonlight /tmp/mlbuild --branch v2", (s, b) =>
                {
                    AnsiConsole.WriteLine(s);
                    return Task.CompletedTask;
                });
            });
            
            ConsoleHelper.Checked("Successfully cloned repository");
        }
        
        // Building moonlight scss
        await ConsoleHelper.Status("Compiling scss", async action =>
        {
            await BashHelper.ExecuteWithOutputHandler("(cd /tmp/mlbuild; cd Moonlight/Styles; bash build.bat;)", (s, b) =>
            {
                AnsiConsole.WriteLine(s);
                return Task.CompletedTask;
            });
        });
        
        ConsoleHelper.Checked("Successfully compiled scss");
        
        // Building moonlight docker image
        await ConsoleHelper.Status("Building docker image", async action =>
        {
            await BashHelper.ExecuteWithOutputHandler("(cd /tmp/mlbuild; docker build -t moonlightpanel/moonlight:custom -f Moonlight/Dockerfile .)", (s, b) =>
            {
                AnsiConsole.WriteLine(s);
                return Task.CompletedTask;
            });
        });
        
        ConsoleHelper.Checked("Successfully built docker image");
    }

    public async Task Update(CallContext context)
    {
        if (!File.Exists("/etc/moonlight/update.flags"))
        {
            ConsoleHelper.Error("Unable to find the update flags. Please run the install command to configure your moonlight installation again");
            return;
        }

        var flags = await File.ReadAllTextAsync("/etc/moonlight/update.flags");
        context.Args.AddRange(flags.Split(" ").Where(x => !string.IsNullOrEmpty(x)));

        await Install(context);
    }

    public async Task Uninstall(CallContext context)
    {
        await ConsoleHelper.Status("Removing moonlight resources", async action =>
        {
            await BashHelper.ExecuteCommand("docker container kill moonlight", ignoreErrors: true);
            await BashHelper.ExecuteCommand("docker container kill moonlight_db", ignoreErrors: true);
            
            await BashHelper.ExecuteCommand("docker container rm moonlight", ignoreErrors: true);
            await BashHelper.ExecuteCommand("docker container rm moonlight_db", ignoreErrors: true);
            
            await BashHelper.ExecuteCommand("docker volume rm moonlight", ignoreErrors: true);
            await BashHelper.ExecuteCommand("docker volume rm moonlight_db", ignoreErrors: true);
            
            await BashHelper.ExecuteCommand("docker image rm moonlightpanel/moonlight:custom", ignoreErrors: true);
            await BashHelper.ExecuteCommand("docker image rm moonlightpanel/moonlight:release", ignoreErrors: true);
        });

        ConsoleHelper.Checked("Successfully removed all moonlight resources");
    }
}