using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.Infrastructure;

internal sealed class ServerDefaultLanguageRequestCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var settings = httpContext.RequestServices.GetService<IServerSettingsService>();
        var language = settings is null
            ? null
            : await settings.GetAsync(ServerSettingKeys.DefaultLanguage, httpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(language))
            language = "en";

        return new ProviderCultureResult(language, language);
    }
}
