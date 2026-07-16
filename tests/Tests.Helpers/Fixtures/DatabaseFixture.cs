using K7.Server.Domain.Constants;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Tests.Helpers.Databases;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Tests.Helpers.Fixtures;

/// <summary>
/// Per-test scope helpers for functional tests. Host lifetime is owned by a
/// [SetUpFixture] in the consuming test assembly (see Application.FunctionalTests/Testing.cs).
/// </summary>
[TestFixture]
public class DatabaseFixture
{
    private static ITestDatabase? _database;
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "NUnit",
        "NUnit1032",
        Justification = "Host lifetime is owned by the consuming assembly SetUpFixture via InitializeAsync/DisposeAsync.")]
    private static CustomWebApplicationFactory? _factory;
    private static IServiceScopeFactory? _scopeFactory;
    public static IServiceScope Scope = null!;
    private static string? _userId;

    public static async Task InitializeAsync()
    {
        if (_factory is not null)
            return;

        _database = await TestDatabaseFactory.CreateAsync();
        _factory = new CustomWebApplicationFactory(_database.GetConnectionString());
        _scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    public static async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
            _database = null;
        }

        _scopeFactory = null;
    }

    [SetUp]
    public async Task DatabaseFixture_SetUp()
    {
        if (_scopeFactory is null)
            throw new InvalidOperationException(
                "Functional test host was not initialized. Ensure the assembly [SetUpFixture] ran and Docker is available for Testcontainers Postgres.");

        Scope = _scopeFactory.CreateScope();
        try
        {
            await _database!.ResetAsync();
        }
        catch (Exception)
        {
        }

        _userId = null;
    }

    [TearDown]
    public void DatabaseFixture_TearDown()
    {
        Scope?.Dispose();
        Scope = null!;
    }

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        var mediator = Scope.ServiceProvider.GetRequiredService<ISender>();
        return await mediator.Send(request);
    }

    public static async Task SendAsync(IBaseRequest request)
    {
        var mediator = Scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(request);
    }

    public static string? GetUserId()
    {
        return _userId;
    }

    public static async Task<string> RunAsDefaultUserAsync()
    {
        return await RunAsUserAsync("test@local", "Testing1234!", []);
    }

    public static async Task<string> RunAsAdministratorAsync()
    {
        return await RunAsUserAsync("administrator@local", "Administrator1234!", [Roles.Administrator]);
    }

    private static async Task<string> RunAsUserAsync(string userName, string password, string[] roles)
    {
        var userManager = Scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = userName, Email = userName };
        var result = await userManager.CreateAsync(user, password);

        if (roles.Any())
        {
            var roleManager = Scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in roles)
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            await userManager.AddToRolesAsync(user, roles);
        }

        if (result.Succeeded)
        {
            _userId = user.Id;
            return _userId;
        }

        var errors = string.Join(Environment.NewLine, result.ToApplicationResult().Errors);
        throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }

    public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    public static async Task<int> CountAsync<TEntity>() where TEntity : class
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<TEntity>().CountAsync();
    }
}
