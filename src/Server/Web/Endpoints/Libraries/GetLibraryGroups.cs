using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.LibraryGroups.Queries.GetLibraryGroups;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetLibraryGroups : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/library-groups", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            var groups = await sender.Send(new GetLibraryGroupsQuery(), cancellationToken);
            return groups.Select(g => g.ToLibraryGroupDto());
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
