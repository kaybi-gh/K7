using K7.Server.Application.Features.IndexedFiles.Commands.RefreshIndexedFile;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class RefreshIndexedFile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/indexed-files/{id}/refresh", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RefreshIndexedFileCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
