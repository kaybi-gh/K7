namespace K7.Server.Domain.Common;

/// <summary>
/// Catalog format ids share <c>SupportedMediaFormatIds</c> with profile and channel tokens.
/// An update that carries only tokens, or nothing, is a failed probe and must not wipe a real list.
/// </summary>
public static class DeviceCapabilityIds
{
    public static bool HasCatalogFormat(IEnumerable<string>? ids)
    {
        if (ids is null)
            return false;

        foreach (var id in ids)
        {
            if (IsCatalogFormat(id))
                return true;
        }

        return false;
    }

    public static List<string> Resolve(IEnumerable<string>? incoming, IEnumerable<string>? existing)
    {
        var next = Clean(incoming);
        if (HasCatalogFormat(next) || !HasCatalogFormat(existing))
            return next;

        return Clean(existing);
    }

    private static bool IsCatalogFormat(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && !id.StartsWith(VideoDecoderProfileTokens.Prefix, StringComparison.Ordinal)
        && !id.StartsWith(AudioOutputChannelTokens.Prefix, StringComparison.Ordinal);

    private static List<string> Clean(IEnumerable<string>? ids) =>
        ids?.Where(static id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
}
