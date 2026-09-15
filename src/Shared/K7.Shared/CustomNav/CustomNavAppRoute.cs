namespace K7.Shared.CustomNav;

public sealed record CustomNavAppRoute(
    string Path,
    string Icon,
    string LabelKey,
    bool AdminOnly = false,
    string? CardColor = null,
    bool NativeOnly = false);
