using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Libraries.Queries.GetLibrary;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetLibrary : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/libraries/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var library = await sender.Send(new GetLibraryQuery(id), cancellationToken);
            return library.ToLibraryDto();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
