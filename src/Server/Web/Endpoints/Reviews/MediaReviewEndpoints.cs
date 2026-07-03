using K7.Server.Application.Features.Reviews.Commands.DeleteMediaReview;
using K7.Server.Application.Features.Reviews.Commands.UpsertMediaReview;
using K7.Server.Application.Features.Reviews.Queries.GetMediaReviews;
using K7.Server.Application.Features.Reviews.Queries.GetMyMediaReview;
using K7.Server.Application.Features.Reviews.Queries.GetMyMediaReviewCount;
using K7.Server.Application.Features.Reviews.Queries.GetMyMediaReviews;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Reviews;

public class UpsertMediaReviewEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/medias/{mediaId:guid}/review", async (
            Guid mediaId,
            [FromBody] UpsertMediaReviewRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new UpsertMediaReviewCommand(mediaId, request), cancellationToken);
            return Results.Ok(id);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetMediaReviewsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{mediaId:guid}/reviews", async (
            Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var reviews = await sender.Send(new GetMediaReviewsQuery(mediaId), cancellationToken);
            return Results.Ok(reviews);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetMyMediaReviewEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{mediaId:guid}/review/me", async (
            Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var state = await sender.Send(new GetMyMediaReviewQuery(mediaId), cancellationToken);
            return state is null ? Results.NotFound() : Results.Ok(state);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class DeleteMediaReviewEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/medias/{mediaId:guid}/review", async (
            Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteMediaReviewCommand(mediaId), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetMyMediaReviewsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/reviews", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var reviews = await sender.Send(new GetMyMediaReviewsQuery(), cancellationToken);
            return Results.Ok(reviews);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetMyMediaReviewCountEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/reviews/count", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var count = await sender.Send(new GetMyMediaReviewCountQuery(), cancellationToken);
            return Results.Ok(count);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
