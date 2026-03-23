using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Authentication;

public class GetServerInfo : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/server-info", async (
            [FromServices] IApplicationDbContext dbContext,
            [FromServices] UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var guestIdentity = await userManager.FindByNameAsync(Roles.Guest);
            var guestEnabled = false;

            if (guestIdentity is not null)
            {
                var guestDomainUser = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.IdentityUserId == guestIdentity.Id, cancellationToken);

                guestEnabled = guestDomainUser?.IsActive == true;
            }

            return Results.Ok(new { guestEnabled });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
