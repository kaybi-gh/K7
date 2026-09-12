using System.Reflection;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace K7.Server.Web.Services;

public sealed class NotificationServerInfo(IConfiguration configuration) : INotificationServerInfo
{
    public string Name =>
        configuration.GetValue<string>("Server:Name") is { Length: > 0 } name ? name : "K7";

    public string Url =>
        configuration.GetValue<string>("BaseUrl")?.TrimEnd('/') ?? "https://localhost:7443";

    public string Version
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (string.IsNullOrWhiteSpace(version))
                return "1.0.0";

            var plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? version[..plus] : version;
        }
    }
}
