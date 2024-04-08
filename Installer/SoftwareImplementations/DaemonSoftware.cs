using Installer.Abstractions;
using Installer.Helpers;

namespace Installer.SoftwareImplementations;

public class DaemonSoftware : ISoftware
{
    public string Name => "Moonlight Daemon";
    public IDependency[] Dependencies { get; } = Array.Empty<IDependency>();
    
    public async Task<bool> CheckFulfilled(CallContext context)
    {
        return false;
    }

    public Task Install(CallContext context)
    {
        throw new NotImplementedException();
    }

    public Task Update(CallContext context)
    {
        throw new NotImplementedException();
    }

    public Task Uninstall(CallContext context)
    {
        throw new NotImplementedException();
    }
}