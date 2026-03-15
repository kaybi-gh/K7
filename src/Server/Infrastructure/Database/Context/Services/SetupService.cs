using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class SetupService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IApplicationDbContext dbContext,
    IServerSettingsService settingsService) : ISetupService
{
    public async Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default)
    {
        return await settingsService.GetAsync(ServerSettingKeys.SetupCompleted, cancellationToken) == true;
    }

    public async Task<Result> CompleteSetupAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (await IsSetupCompletedAsync(cancellationToken))
            return Result.Failure(["Setup has already been completed."]);

        var adminResult = await CreateAdminAsync(email, password, cancellationToken);
        if (!adminResult.Succeeded)
            return adminResult;

        await settingsService.SetAsync(ServerSettingKeys.SetupCompleted, true, cancellationToken);
        return Result.Success();
    }

    private async Task<Result> CreateAdminAsync(string email, string password, CancellationToken cancellationToken)
    {
        var identityUser = new ApplicationUser { UserName = email, Email = email };
        var createResult = await userManager.CreateAsync(identityUser, password);

        if (!createResult.Succeeded)
            return Result.Failure(createResult.Errors.Select(e => e.Description));

        await EnsureRoleAsync(Roles.Administrator);
        await userManager.AddToRoleAsync(identityUser, Roles.Administrator);

        dbContext.Users.Add(new User { IdentityUserId = identityUser.Id });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }
}
