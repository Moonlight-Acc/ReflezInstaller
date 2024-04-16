using Newtonsoft.Json;

namespace Installer.Models;

public class BasicPanelConfigModel
{
    [JsonProperty("Reflez")]
    public ReflezData Moonlight { get; set; } = new();
    
    public class ReflezData
    {
        [JsonProperty("AppUrl")]
        public string AppUrl { get; set; }

        [JsonProperty("Database")] public DatabaseData Database { get; set; } = new();
    }
    
    public class DatabaseData
    {
        [JsonProperty("Host")]
        public string Host { get; set; }
        
        [JsonProperty("Port")]
        public int Port { get; set; }
        
        [JsonProperty("Username")]
        public string Username { get; set; }
        
        [JsonProperty("Password")]
        public string Password { get; set; }
        
        [JsonProperty("Database")]
        public string Database { get; set; }
    }
}
