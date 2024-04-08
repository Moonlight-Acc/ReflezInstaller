using Spectre.Console;

namespace Installer.Helpers;

public class CallContext
{
    public List<string> Args { get; }
    public DynamicStorage Storage { get; } = new();
    
    public CallContext(List<string> args)
    {
        Args = args;
    }

    public T? GetParameterValue<T>(string name)
    {
        if (!HasParameter(name))
            return default;

        return GetRequiredParameterValue<T>(name);
    }

    public T GetRequiredParameterValue<T>(string name)
    {
        if (!HasParameter(name))
        {
            AnsiConsole.MarkupLine($"[red]The '{name}' parameter is missing but required[/]");
            Environment.Exit(1);
        }
        
        var indexId = Args.IndexOf(name);
        var value = Args[indexId + 1];

        if (typeof(T) == typeof(string))
            return (T)Convert.ChangeType(value, typeof(T));
        else if (typeof(T) == typeof(int))
            return (T)Convert.ChangeType(int.Parse(value), typeof(T));
        else if (typeof(T) == typeof(bool))
            return (T)Convert.ChangeType(bool.Parse(value), typeof(T));
        else
            throw new NotImplementedException($"The parameter type {typeof(T).Name} has not been implemented yet");
    }

    public bool HasParameter(string name)
    {
        var indexId = Args.IndexOf(name);
        
        if (indexId == -1)
            return false;

        if (Args.Count - 1 < indexId + 1)
            return false;

        return true;
    }

    public bool HasFlag(string name) => Args.Contains(name);
}