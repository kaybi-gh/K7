namespace K7.Clients.Shared.Models;

public enum K7DialogMaxWidth
{
    ExtraExtraSmall,
    ExtraSmall,
    Small,
    Medium,
    Large,
    ExtraLarge,
    False
}

public sealed class K7DialogOptions
{
    public K7DialogMaxWidth MaxWidth { get; init; } = K7DialogMaxWidth.Small;
    public bool FullWidth { get; init; }
    public bool CloseOnEscapeKey { get; init; } = true;
    public bool CloseButton { get; init; } = true;
    public bool BackdropClick { get; init; } = true;
}
