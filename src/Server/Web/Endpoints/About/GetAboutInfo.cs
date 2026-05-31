using System.Reflection;
using K7.Shared.Dtos;

namespace K7.Server.Web.Endpoints.About;

public class GetAboutInfo : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/about", () =>
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? "unknown";

            return Results.Ok(new AboutInfoDto
            {
                ServerVersion = version
            });
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
