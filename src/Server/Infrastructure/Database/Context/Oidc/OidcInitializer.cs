using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Infrastructure.Database.Context.Oidc;
public static class OidcInitializer
{
    public static async Task InitializeOidcClientsAsync(this WebApplication app)
    {
        // TODO - Configure BFF auth
        await using var scope = app.Services.CreateAsyncScope();

        await RegisterApplicationsAsync(scope.ServiceProvider);
        await RegisterScopesAsync(scope.ServiceProvider);

        static async Task RegisterApplicationsAsync(IServiceProvider serviceProvider)
        {
            var manager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            if (await manager.FindByClientIdAsync("k7-web") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    DisplayName = "K7 Web client",
                    ClientId = "k7-web",
                    ClientSecret = "k7-web-secret",
                    ConsentType = ConsentTypes.Explicit,
                    ClientType = ClientTypes.Confidential,
                    RedirectUris = { new Uri("https://localhost:7123/callback/login") }, // TODO - Env
                    PostLogoutRedirectUris = { new Uri("https://localhost:7123/callback/logout") },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.EndSession,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.RefreshToken,
                        Permissions.ResponseTypes.Code,
                        Permissions.Scopes.Email,
                        Permissions.Scopes.Profile,
                        Permissions.Scopes.Roles,
                        Permissions.Prefixes.Scope + "api"
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                });
            }

            if (await manager.FindByClientIdAsync("k7-maui") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    DisplayName = "K7 MAUI client",
                    ClientId = "k7-maui",
                    ConsentType = ConsentTypes.Explicit,
                    ClientType = ClientTypes.Public,
                    RedirectUris = { new Uri("http://localhost"), new Uri("http://localhost:59451"), new Uri("k7://login-callback") },
                    PostLogoutRedirectUris = { new Uri("http://localhost")/*, new Uri("k7://logout-callback")*/ },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.EndSession,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.RefreshToken,
                        Permissions.ResponseTypes.Code,
                        Permissions.Scopes.Email,
                        Permissions.Scopes.Profile,
                        Permissions.Scopes.Roles,
                        Permissions.Prefixes.Scope + "api"
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                });
            }
        }

        static async Task RegisterScopesAsync(IServiceProvider serviceProvider)
        {
            var manager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();

            if (await manager.FindByNameAsync("api") == null)
            {
                await manager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    DisplayName = "K7 API access",
                    DisplayNames =
                    {
                        [CultureInfo.GetCultureInfo("fr-FR")] = "Accès à l'API"
                    },
                    Name = "api",
                    Resources =
                    {
                        "k7-server"
                    }
                });
            }
        }
    }
}
