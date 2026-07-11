namespace K7.Server.Infrastructure.Configuration;

public class DatabaseConfiguration
{
    public string Provider { get; set; } = "";
    public string UserID { get; set; } = "";
    public string Password { get; set; } = "";
    public string Server { get; set; } = "";
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = "";
    public int MaxPoolSize { get; set; } = 50;
}
