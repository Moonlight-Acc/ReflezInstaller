using System.ComponentModel;
using MoonCore.Helpers;
using Newtonsoft.Json;

namespace Installer.Models;

public class CoreConfiguration
{
    [JsonProperty("AppUrl")]
    public string AppUrl { get; set; } = "";

    [JsonProperty("Http")] public HttpData Http { get; set; } = new();
    [JsonProperty("Database")] public DatabaseData Database { get; set; } = new();

    public class HttpData
    {
        [JsonProperty("HttpPort")]
        public int HttpPort { get; set; } = 80;
        
        [JsonProperty("HttpsPort")]
        public int HttpsPort { get; set; } = 443;
        
        [JsonProperty("EnableSsl")]
        public bool EnableSsl { get; set; } = false;

        [JsonProperty("CertPath")]
        public string CertPath { get; set; } = "";
        
        [JsonProperty("KeyPath")]
        public string KeyPath { get; set; } = "";
    }
    
    public class DatabaseData
    {
        [JsonProperty("Host")] public string Host { get; set; } = "INSTALLER_ERROR";

        [JsonProperty("Port")] public int Port { get; set; } = 9999;

        [JsonProperty("Username")] public string Username { get; set; } = "INSTALLER_ERROR";

        [JsonProperty("Password")] public string Password { get; set; } = "INSTALLER_ERROR";

        [JsonProperty("Database")] public string Database { get; set; } = "INSTALLER_ERROR";
    }
}