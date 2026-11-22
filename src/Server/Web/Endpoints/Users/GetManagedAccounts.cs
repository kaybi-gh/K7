using K7.Server.Application.Features.Users.Queries.GetManagedAccounts;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetManagedAccounts : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/managed-accounts", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetManagedAccountsQuery(), cancellationToken);
            return Results.Ok(result.Accounts.Select(account =>
            {
                var avatarUrl = result.AvatarPictureIds.TryGetValue(account.Id, out var pictureId)
                    ? $"/api/metadata-pictures/{pictureId}"
                    : null;
                return account with { AvatarUrl = avatarUrl };
            }));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
