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

            if (await manager.FindByClientIdAsync("k7-native") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    DisplayName = "K7 native client",
                    ClientId = "k7-native",
                    ConsentType = ConsentTypes.Implicit,
                    ClientType = ClientTypes.Public,
                    RedirectUris = { new Uri("http://localhost"), new Uri("http://localhost:59451"), new Uri("k7://login-callback") },
                    PostLogoutRedirectUris = { new Uri("http://localhost") },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.DeviceAuthorization,
                        Permissions.Endpoints.EndSession,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.DeviceCode,
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
