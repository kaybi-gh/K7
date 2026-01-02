using K7.Server.Application.Features.Libraries.Queries.GetLibraries;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetLibraries : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/libraries", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            var libraries = await sender.Send(new GetLibrariesQuery(), cancellationToken);
            return libraries.Select(LibraryDto.FromDomain);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
