using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class CreateLibrary : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/libraries", async ([FromServices] ISender sender, CreateLibraryCommand command, CancellationToken cancellationToken) =>
        {
            return await sender.Send(command, cancellationToken);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName)
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Test";
            return Task.CompletedTask;
        });
    }
}
