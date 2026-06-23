namespace K7.Shared.Dtos;

public sealed record AudioMuseAiSettingsDto
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
