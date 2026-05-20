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

    public async Task<Result> CompleteSetupWithExternalLoginAsync(string email, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
    {
        if (await IsSetupCompletedAsync(cancellationToken))
            return Result.Failure(["Setup has already been completed."]);

        var adminResult = await CreateAdminWithExternalLoginAsync(email, loginProvider, providerKey, cancellationToken);
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

    private async Task<Result> CreateAdminWithExternalLoginAsync(string email, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        var identityUser = await userManager.FindByLoginAsync(loginProvider, providerKey)
            ?? await userManager.FindByEmailAsync(email);

        if (identityUser is null)
        {
            identityUser = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var createResult = await userManager.CreateAsync(identityUser);

            if (!createResult.Succeeded)
                return Result.Failure(createResult.Errors.Select(e => e.Description));

            var loginResult = await userManager.AddLoginAsync(identityUser, new UserLoginInfo(loginProvider, providerKey, loginProvider));
            if (!loginResult.Succeeded)
                return Result.Failure(loginResult.Errors.Select(e => e.Description));
        }

        await EnsureRoleAsync(Roles.Administrator);
        if (!await userManager.IsInRoleAsync(identityUser, Roles.Administrator))
            await userManager.AddToRoleAsync(identityUser, Roles.Administrator);

        var domainUserExists = await dbContext.Users
            .AnyAsync(u => u.IdentityUserId == identityUser.Id, cancellationToken);

        if (!domainUserExists)
        {
            dbContext.Users.Add(new User { IdentityUserId = identityUser.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }
}
