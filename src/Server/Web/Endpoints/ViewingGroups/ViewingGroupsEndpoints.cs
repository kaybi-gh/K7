using K7.Server.Application.Features.ViewingGroups.Commands.CreateViewingGroup;
using K7.Server.Application.Features.ViewingGroups.Commands.DeleteViewingGroup;
using K7.Server.Application.Features.ViewingGroups.Commands.SetViewingGroupPin;
using K7.Server.Application.Features.ViewingGroups.Commands.UpdateViewingGroup;
using K7.Server.Application.Features.ViewingGroups.Queries.GetViewingGroupMemberCandidates;
using K7.Server.Application.Features.ViewingGroups.Queries.GetViewingGroups;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.ViewingGroups;

public class GetViewingGroups : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/viewing-groups", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetViewingGroupsQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetViewingGroupMemberCandidates : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/viewing-groups/member-candidates", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetViewingGroupMemberCandidatesQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class CreateViewingGroup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/viewing-groups", async (
            [FromServices] ISender sender,
            [FromBody] CreateViewingGroupRequest request,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateViewingGroupCommand
            {
                Name = request.Name,
                HostUserId = request.HostUserId,
                MemberUserIds = request.MemberUserIds,
                Pin = request.Pin
            }, cancellationToken);
            return Results.Created($"/api/viewing-groups/{id}", id);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateViewingGroup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/viewing-groups/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdateViewingGroupRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateViewingGroupCommand
            {
                Id = id,
                Name = request.Name,
                HostUserId = request.HostUserId,
                MemberUserIds = request.MemberUserIds
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class DeleteViewingGroup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/viewing-groups/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteViewingGroupCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class SetViewingGroupPin : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/viewing-groups/{id:guid}/pin", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] SetViewingGroupPinRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new SetViewingGroupPinCommand(id, request.Pin), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
