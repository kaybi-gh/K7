using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.Database.Context.Data;

public static class DatabaseInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
        await initializer.InitializeAsync();
        await initializer.SeedAsync();
    }
}

public class ApplicationDbContextInitializer(
    ILogger<ApplicationDbContextInitializer> logger,
    ApplicationDbContext context,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IServerSettingsService settingsService,
    ISetupService setupService,
    ISetupTokenProvider setupTokenProvider,
    IMediaLibraryAvailabilityService mediaLibraryAvailabilityService)
{
    public async Task InitializeAsync()
    {
        try
        {
            await context.Database.MigrateAsync();
            await mediaLibraryAvailabilityService.EnsurePopulatedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedRolesAsync();
            await SeedGuestUserAsync();
            await MigrateExistingAdminAsync();
            await AutoSetupFromEnvAsync();
            await EnsureSetupTokenAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = [Roles.Administrator, Roles.User, Roles.Guest];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private async Task SeedGuestUserAsync()
    {
        var existingGuest = await userManager.FindByNameAsync(Roles.Guest);
        if (existingGuest is not null)
            return;

        var guestIdentity = new ApplicationUser { UserName = Roles.Guest, Email = "guest@k7.local" };
        var result = await userManager.CreateAsync(guestIdentity);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed guest user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(guestIdentity, Roles.Guest);
        context.Users.Add(new K7.Server.Domain.Entities.Users.User { IdentityUserId = guestIdentity.Id });
        await context.SaveChangesAsync();
        logger.LogInformation("Guest user seeded.");
    }

    private async Task MigrateExistingAdminAsync()
    {
        if (await settingsService.GetAsync(ServerSettingKeys.SetupCompleted) == true)
            return;

        var admins = await userManager.GetUsersInRoleAsync(Roles.Administrator);
        if (admins.Count > 0)
        {
            logger.LogInformation("Existing administrator found - marking setup as completed.");
            await settingsService.SetAsync(ServerSettingKeys.SetupCompleted, true);
        }
    }

    private async Task AutoSetupFromEnvAsync()
    {
        if (await setupService.IsSetupCompletedAsync())
            return;

        var email = Environment.GetEnvironmentVariable("K7_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("K7_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        logger.LogInformation("K7_ADMIN_EMAIL and K7_ADMIN_PASSWORD detected - completing setup automatically.");
        var result = await setupService.CompleteSetupAsync(email, password);

        if (!result.Succeeded)
            logger.LogError("Auto-setup failed: {Errors}", string.Join(", ", result.Errors));
    }

    private async Task EnsureSetupTokenAsync()
    {
        if (await setupService.IsSetupCompletedAsync())
            return;

        var existingHash = await settingsService.GetAsync(ServerSettingKeys.SetupTokenHash);
        if (!string.IsNullOrWhiteSpace(existingHash))
            return;

        var token = Environment.GetEnvironmentVariable("K7_SETUP_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            token = SetupTokenHelper.GenerateToken();

        await settingsService.SetAsync(ServerSettingKeys.SetupTokenHash, SetupTokenHelper.HashToken(token));
        setupTokenProvider.SetToken(token);
        logger.LogWarning("K7 setup token required for initial admin creation: {SetupToken}", token);
    }
}
