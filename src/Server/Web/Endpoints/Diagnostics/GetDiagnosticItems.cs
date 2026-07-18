using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Web.Converters;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class GetDiagnosticItems : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/diagnostics/items", async ([FromServices] ISender sender,
            [FromQuery] Guid? libraryId,
            [FromQuery] DiagnosticEntityType? entityType,
            [FromQuery(Name = "issue")] DiagnosticIssue[]? issues,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = PagingDefaults.DefaultPageSize,
            CancellationToken cancellationToken = default) =>
        {
            var query = new GetDiagnosticItemsQuery
            {
                LibraryId = libraryId,
                EntityType = entityType,
                Issues = issues is { Length: > 0 } ? issues : null,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
            var result = await sender.Send(query, cancellationToken);
            return result.ToDto(item => item);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
