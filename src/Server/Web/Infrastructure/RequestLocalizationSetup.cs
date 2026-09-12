using K7.Shared;
using Microsoft.AspNetCore.Localization;

namespace K7.Server.Web.Infrastructure;

internal static class RequestLocalizationSetup
{
    public static RequestLocalizationOptions CreateOptions()
    {
        var supportedCultures = SupportedLanguages.Interface.Select(l => l.Code).ToArray();
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture("en")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        for (var i = options.RequestCultureProviders.Count - 1; i >= 0; i--)
        {
            if (options.RequestCultureProviders[i] is AcceptLanguageHeaderRequestCultureProvider)
                options.RequestCultureProviders.RemoveAt(i);
        }

        options.RequestCultureProviders.Add(new ServerDefaultLanguageRequestCultureProvider
        {
            Options = options
        });

        return options;
    }
}
