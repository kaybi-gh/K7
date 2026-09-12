using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class ServerDefaultLanguageRequestCultureProviderTests
{
    [Test]
    public async Task DetermineProviderCultureResult_ShouldUseAdminDefault_WhenSettingIsFrench()
    {
        var context = CreateContext("fr");
        var provider = new ServerDefaultLanguageRequestCultureProvider();

        var result = await provider.DetermineProviderCultureResult(context);

        result.Should().NotBeNull();
        result!.UICultures[0].Value.Should().Be("fr");
        result.Cultures[0].Value.Should().Be("fr");
    }

    [Test]
    public async Task DetermineProviderCultureResult_ShouldFallBackToEnglish_WhenSettingIsMissing()
    {
        var context = CreateContext(null);
        var provider = new ServerDefaultLanguageRequestCultureProvider();

        var result = await provider.DetermineProviderCultureResult(context);

        result.Should().NotBeNull();
        result!.UICultures[0].Value.Should().Be("en");
    }

    [Test]
    public async Task CreateOptions_ShouldPreferCookieOverAdminDefault()
    {
        var context = CreateContext("fr", cookieCulture: "en");

        var culture = await ResolveCultureAsync(context);

        culture.Should().Be("en");
    }

    [Test]
    public async Task CreateOptions_ShouldUseAdminDefault_WhenNoCookie()
    {
        var context = CreateContext("fr");
        context.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";

        var culture = await ResolveCultureAsync(context);

        culture.Should().Be("fr");
    }

    [Test]
    public void CreateOptions_ShouldOmitAcceptLanguageProvider()
    {
        var options = RequestLocalizationSetup.CreateOptions();

        options.RequestCultureProviders.Should().HaveCount(3);
        options.RequestCultureProviders[0].Should().BeOfType<QueryStringRequestCultureProvider>();
        options.RequestCultureProviders[1].Should().BeOfType<CookieRequestCultureProvider>();
        options.RequestCultureProviders[2].Should().BeOfType<ServerDefaultLanguageRequestCultureProvider>();
    }

    private static async Task<string> ResolveCultureAsync(HttpContext context)
    {
        var options = RequestLocalizationSetup.CreateOptions();
        foreach (var provider in options.RequestCultureProviders)
        {
            var result = await provider.DetermineProviderCultureResult(context);
            if (result is not null && result.UICultures.Count > 0)
                return result.UICultures[0].Value ?? options.DefaultRequestCulture.UICulture.TwoLetterISOLanguageName;
        }

        return options.DefaultRequestCulture.UICulture.TwoLetterISOLanguageName;
    }

    private static DefaultHttpContext CreateContext(string? language, string? cookieCulture = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new StubServiceProvider(new StubServerSettings(language))
        };

        if (!string.IsNullOrEmpty(cookieCulture))
        {
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cookieCulture));
            context.Request.Headers.Cookie =
                $"{CookieRequestCultureProvider.DefaultCookieName}={cookieValue}";
        }

        return context;
    }

    private sealed class StubServerSettings(string? language) : IServerSettingsService
    {
        public Task<T?> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
        {
            if (key.Name != ServerSettingKeys.DefaultLanguage.Name)
                return Task.FromResult(key.DefaultValue);

            return Task.FromResult((T?)(object?)language);
        }

        public Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<object?> GetAsync(ISettingKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(language);

        public Task SetAsync(ISettingKey key, object value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubServiceProvider(IServerSettingsService settings) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServerSettingsService) ? settings : null;
    }
}
