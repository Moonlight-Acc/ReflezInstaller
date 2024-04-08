using Installer.Abstractions;
using Installer.Helpers;
using Spectre.Console;

namespace Installer.DependencyImplementations;

public class DockerDependency : IDependency
{
    public string Name => "Docker";
    
    public async Task<bool> CheckFulfilled(CallContext callContext)
    {
        return await BashHelper.ExecuteCommand("which docker", ignoreErrors: true) != "";
    }

    public async Task Fulfill(CallContext callContext)
    {
        await ConsoleHelper.Status("Running installation script", async action =>
        {
            await BashHelper.ExecuteWithOutputHandler("curl -sSL https://get.docker.com/ | CHANNEL=stable bash", (s, b) =>
            {
                AnsiConsole.WriteLine(s);
                return Task.CompletedTask;
            });
        });
    }
}