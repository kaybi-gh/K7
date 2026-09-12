namespace K7.Shared.Dtos.Notifications;

public sealed record NotificationWebhookPresetDto
{
    public required string Id { get; init; }
    public required string DisplayNameKey { get; init; }
    public required string UrlHint { get; init; }
    public required string Method { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public required string RawJsonTemplate { get; init; }
}
