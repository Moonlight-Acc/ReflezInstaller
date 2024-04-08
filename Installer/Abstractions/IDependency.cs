using Installer.Helpers;

namespace Installer.Abstractions;

public interface IDependency
{
    public string Name { get; }

    public Task<bool> CheckFulfilled(CallContext callContext);
    public Task Fulfill(CallContext callContext);
}