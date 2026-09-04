using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Helpers;

public static class TrailerPlaybackHelper
{
    public static TrailerDto? Pick(IReadOnlyList<TrailerDto>? trailers)
    {
        if (trailers is not { Count: > 0 })
            return null;

        return trailers.FirstOrDefault(trailer => trailer.Type == "Trailer") ?? trailers[0];
    }

    /// <summary>
    /// Non-embeddable sites always leave K7. YouTube stays in the overlay unless
    /// the user opted into the system app on native or TV (web desktop/phone stays in K7).
    /// </summary>
    public static bool ShouldOpenExternally(
        ClientType clientType,
        DeviceType deviceType,
        string? site,
        bool openTrailersExternally) =>
        !IsEmbeddable(site)
        || (openTrailersExternally && (clientType == ClientType.Native || deviceType == DeviceType.TV));

    public static string? TryBuildWatchUrl(string? site, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var trimmed = key.Trim();
        if (IsHttpUrl(trimmed, out var absolute))
            return absolute;

        if (!IsEmbeddable(site))
            return null;

        var id = Uri.EscapeDataString(trimmed);
        return $"https://www.youtube.com/watch?v={id}";
    }

    public static string? TryBuildEmbedUrl(string? site, string? key)
    {
        if (!IsEmbeddable(site) || string.IsNullOrWhiteSpace(key))
            return null;

        var trimmed = key.Trim();
        if (IsHttpUrl(trimmed, out _))
            return null;

        var id = Uri.EscapeDataString(trimmed);
        return $"https://www.youtube.com/embed/{id}?autoplay=1&playsinline=1&rel=0&modestbranding=1";
    }

    public static bool IsEmbeddable(string? site) =>
        NormalizeSite(site) is "youtube";

    private static string NormalizeSite(string? site) =>
        string.IsNullOrWhiteSpace(site) ? "youtube" : site.Trim().ToLowerInvariant();

    private static bool IsHttpUrl(string value, out string absolute)
    {
        absolute = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return false;

        absolute = uri.AbsoluteUri;
        return true;
    }
}
