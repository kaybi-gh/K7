using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
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

public class ApplicationDbContextInitializer
{
    private readonly ILogger<ApplicationDbContextInitializer> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitializer(ILogger<ApplicationDbContextInitializer> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // Default roles
        var administratorRole = new IdentityRole(Roles.Administrator);

        if (await _roleManager.Roles.AllAsync(r => r.Name != administratorRole.Name))
        {
            await _roleManager.CreateAsync(administratorRole);
        }

        // Default users
        var administrator = new ApplicationUser { UserName = "administrator@localhost", Email = "administrator@localhost" };

        if (await _userManager.Users.AllAsync(u => u.UserName != administrator.UserName))
        {
            // Create identity
            var result = await _userManager.CreateAsync(administrator, "Administrator1!");
            
            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(administratorRole.Name))
                {
                    await _userManager.AddToRolesAsync(administrator, [administratorRole.Name]);
                }
            }

            // Create domain user
            var adminIdentityUser = await _userManager.FindByNameAsync("administrator@localhost");
            if (adminIdentityUser is not null)
            {
                var existingDomainAdmin = await _context.Users
                    .SingleOrDefaultAsync(u => u.IdentityUserId == adminIdentityUser.Id);

                if (existingDomainAdmin is null)
                {
                    var displayName = adminIdentityUser.Email ?? adminIdentityUser.UserName ?? adminIdentityUser.Id;

                    var domainAdmin = new User
                    {
                        IdentityUserId = adminIdentityUser.Id,
                        DisplayName = displayName
                    };

                    _context.Users.Add(domainAdmin);
                    await _context.SaveChangesAsync();
                }
            }
        }        
    }
}
