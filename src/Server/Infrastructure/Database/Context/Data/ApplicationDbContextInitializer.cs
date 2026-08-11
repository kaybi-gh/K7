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

        var guestIdentity = new ApplicationUser { UserName = Roles.Guest, Email = null };
        var result = await userManager.CreateAsync(guestIdentity);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed guest user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(guestIdentity, Roles.Guest);
        context.Users.Add(new K7.Server.Domain.Entities.Users.User
        {
            IdentityUserId = guestIdentity.Id,
            IsActive = false
        });
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

        var userName = Environment.GetEnvironmentVariable("K7_ADMIN_USERNAME")
            ?? Environment.GetEnvironmentVariable("K7_ADMIN_EMAIL");
        var email = Environment.GetEnvironmentVariable("K7_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("K7_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return;

        var storedEmail = !string.IsNullOrWhiteSpace(email) && email.Contains('@') ? email : null;

        logger.LogInformation("Admin bootstrap credentials detected - completing setup automatically.");
        var result = await setupService.CompleteSetupAsync(userName, password, storedEmail);

        if (!result.Succeeded)
            logger.LogError("Auto-setup failed: {Errors}", string.Join(", ", result.Errors));
    }

    private async Task EnsureSetupTokenAsync()
    {
        if (await setupService.IsSetupCompletedAsync())
            return;

        var existingHash = await settingsService.GetAsync(ServerSettingKeys.SetupTokenHash);
        var storedToken = await settingsService.GetAsync(ServerSettingKeys.SetupToken);
        var envToken = Environment.GetEnvironmentVariable("K7_SETUP_TOKEN");

        // Env always wins when set: operators pin K7_SETUP_TOKEN and expect it to be honored.
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            if (!string.IsNullOrWhiteSpace(existingHash)
                && SetupTokenHelper.VerifyToken(envToken, existingHash))
            {
                await PersistAndLogSetupTokenAsync(envToken, existingHash);
            }
            else
            {
                await PersistAndLogSetupTokenAsync(envToken, hash: null);
            }

            return;
        }

        // Re-log the same token on every restart until setup completes.
        if (!string.IsNullOrWhiteSpace(existingHash)
            && !string.IsNullOrWhiteSpace(storedToken)
            && SetupTokenHelper.VerifyToken(storedToken, existingHash))
        {
            await PersistAndLogSetupTokenAsync(storedToken, existingHash);
            return;
        }

        // First boot, or legacy hash-only installs without recoverable plaintext: mint a fresh token.
        var token = SetupTokenHelper.GenerateToken();
        await PersistAndLogSetupTokenAsync(token, hash: null);
    }

    private async Task PersistAndLogSetupTokenAsync(string token, string? hash)
    {
        var tokenHash = hash ?? SetupTokenHelper.HashToken(token);
        await settingsService.SetAsync(ServerSettingKeys.SetupTokenHash, tokenHash);
        await settingsService.SetAsync(ServerSettingKeys.SetupToken, token);
        setupTokenProvider.SetToken(token);
        LogSetupTokenBanner(token);
    }

    private void LogSetupTokenBanner(string token)
    {
        // Literal "K7_SETUP_TOKEN=" so operators can grep docker/k8s logs for the env var name they expect.
        logger.LogWarning("");
        logger.LogWarning("============================================================");
        logger.LogWarning("K7 first-run setup required. Use this token on /setup:");
        logger.LogWarning("K7_SETUP_TOKEN={SetupToken}", token);
        logger.LogWarning("============================================================");
        logger.LogWarning("");
    }
}
