namespace K7.Shared.Dtos;

public sealed record MusicHitParadeDto
{
    public string Period { get; init; } = MusicHitParadePeriods.All;
    public string Scope { get; init; } = MusicHitParadeScopes.Personal;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public IReadOnlyList<PlayedMusicTrackDto> Tracks { get; init; } = [];
}

public static class MusicHitParadePeriods
{
    public const string All = "all";
    public const string Year = "year";
    public const string Season = "season";
    public const string Month = "month";
    public const string Custom = "custom";
}

public static class MusicHitParadeScopes
{
    public const string Personal = "personal";
    public const string Server = "server";
}

public static class MusicHitParadeSeasons
{
    public const string Winter = "winter";
    public const string Spring = "spring";
    public const string Summer = "summer";
    public const string Autumn = "autumn";
}
