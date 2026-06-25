using System.Security.Claims;
using System.Text.Encodings.Web;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Services;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyService apiKeyService)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var apiKeyValue = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(apiKeyValue))
            return AuthenticateResult.NoResult();

        var validatedKey = await apiKeyService.ValidateKeyAsync(apiKeyValue, Context.RequestAborted);
        if (validatedKey is null)
            return AuthenticateResult.Fail("Invalid or expired API key.");

        var role = validatedKey.Key.Scope switch
        {
            ApiKeyScope.Admin => Roles.Administrator,
            ApiKeyScope.Write => Roles.User,
            _ => Roles.Guest
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, validatedKey.IdentityUserId),
            new Claim(ClaimTypes.Name, $"ApiKey:{validatedKey.Key.Name}"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
