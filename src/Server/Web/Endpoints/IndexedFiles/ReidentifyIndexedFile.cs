using K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class ReidentifyIndexedFile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/indexed-files/{id}/reidentify", async (
            [FromServices] ISender sender, 
            Guid id, 
            [FromBody] ReidentifyIndexedFileRequest request, 
            CancellationToken cancellationToken) =>  
        {
            await sender.Send(new ReidentifyIndexedFileCommand
            {
                IndexedFileId = id,
                SelectedProvider = request.SelectedProvider,
                SelectedExternalId = request.SelectedExternalId
            }, cancellationToken);
            
            return Results.NoContent();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}