using System.ComponentModel;
using MoonCore.Helpers;
using Newtonsoft.Json;

namespace Installer.Models;

public class CoreConfiguration
{
    [JsonProperty("AppUrl")]
    [Description("This defines the public url of moonlight. This will be used e.g. by the nodes to communicate with moonlight")]
    public string AppUrl { get; set; } = "";

    [JsonProperty("Http")] public HttpData Http { get; set; } = new();
    [JsonProperty("Database")] public DatabaseData Database { get; set; } = new();

    public class HttpData
    {
        [Description("The port moonlight should listen to http requests")]
        [JsonProperty("HttpPort")]
        public int HttpPort { get; set; } = 80;
        
        [Description("The port moonlight should listen to https requests if ssl is enabled")]
        [JsonProperty("HttpsPort")]
        public int HttpsPort { get; set; } = 443;
        
        [Description("Enables the use of an ssl certificate which is required in order to acceppt https requests")]
        [JsonProperty("EnableSsl")]
        public bool EnableSsl { get; set; } = false;

        [Description("Specifies the location of the certificate .pem file to load")]
        [JsonProperty("CertPath")]
        public string CertPath { get; set; } = "";
        
        [Description("Specifies the location of the key .pem file to load")]
        [JsonProperty("KeyPath")]
        public string KeyPath { get; set; } = "";
    }
    
    public class DatabaseData
    {
        [JsonProperty("Host")] public string Host { get; set; } = "your.db.host";

        [JsonProperty("Port")] public int Port { get; set; } = 3306;

        [JsonProperty("Username")] public string Username { get; set; } = "moonlight_user";

        [JsonProperty("Password")] public string Password { get; set; } = "s3cr3t";

        [JsonProperty("Database")] public string Database { get; set; } = "moonlight_db";
    }
}