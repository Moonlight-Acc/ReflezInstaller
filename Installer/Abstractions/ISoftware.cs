using Installer.Helpers;

namespace Installer.Abstractions;

public interface ISoftware
{
    public string Name { get; }
    public string Id { get; }
    public IDependency[] Dependencies { get; }

    public Task<bool> CheckFulfilled(CallContext context);
    public Task Install(CallContext context);
    public Task Update(CallContext context);
    public Task Uninstall(CallContext context);
}