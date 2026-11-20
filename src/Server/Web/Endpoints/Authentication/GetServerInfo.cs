using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Shared.Dtos;
using K7.Shared.Theming;
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
            [FromServices] IPasswordPolicyService passwordPolicy,
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
            var serverThemes = ServerThemes.Parse(await serverSettings.GetAsync(ServerSettingKeys.ServerThemes, cancellationToken));
            var defaultCards = MediaCardStyles.Parse(await serverSettings.GetAsync(ServerSettingKeys.DefaultCardStyle, cancellationToken));
            var defaultSkins = AppearanceSkins.ParseSet(await serverSettings.GetAsync(ServerSettingKeys.DefaultSkins, cancellationToken));
            var policy = await passwordPolicy.GetAsync(cancellationToken);

            return Results.Ok(new ServerInfoDto
            {
                GuestEnabled = guestEnabled,
                DefaultLanguage = defaultLanguage,
                DefaultTheme = defaultTheme,
                ServerThemes = serverThemes,
                DefaultCards = defaultCards,
                // The shared set stays where clients from before the sets per device read it.
                DefaultSkins = defaultSkins?.Default,
                DefaultSkinsByDevice = defaultSkins is { DeviceOverrides.Count: > 0 } ? defaultSkins.DeviceOverrides : null,
                PasswordPolicy = policy
            });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
