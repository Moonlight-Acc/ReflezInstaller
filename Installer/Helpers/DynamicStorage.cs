namespace Installer.Helpers;

public class DynamicStorage
{
    private readonly Dictionary<string, object> Data = new();

    public void Set(string id, object data) => Data[id] = data;
    public object Get(string id) => Data[id];
    public T Get<T>(string id) => (T)Data[id];
}