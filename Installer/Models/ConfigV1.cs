namespace Installer.Models;

public class ConfigV1
{
    public RemoteData Remote { get; set; } = new();
    public FtpData Ftp { get; set; } = new();
    public HttpData Http { get; set; } = new();
    public DockerData Docker { get; set; } = new();
    
    public class DockerData
    {
        public List<string> DnsServers { get; set; } = new();
    }
    
    public class FtpData
    {
        public int Port { get; set; } = 2021;
    }
    
    public class RemoteData
    {
        public string Url { get; set; } = "http://localhost:5132/";
        public string Token { get; set; } = "";
    }
    
    public class HttpData
    {
        public bool UseSsl { get; set; } = false;
        public int HttpPort { get; set; } = 8080;
        public string Fqdn { get; set; } = "";
    }
}