using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Shared.Dtos;
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
            [FromServices] IServerSettingsService serverSettings,
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

            var defaultLanguage = await serverSettings.GetAsync(ServerSettingKeys.DefaultLanguage, cancellationToken) ?? "en";
            var defaultTheme = await serverSettings.GetAsync(ServerSettingKeys.DefaultTheme, cancellationToken) ?? "default-dark";

            return Results.Ok(new ServerInfoDto
            {
                GuestEnabled = guestEnabled,
                DefaultLanguage = defaultLanguage,
                DefaultTheme = defaultTheme
            });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
