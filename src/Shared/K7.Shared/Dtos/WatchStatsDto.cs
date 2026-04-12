namespace K7.Shared.Dtos;

public sealed record WatchStatsDto
{
    public string Period { get; init; } = "month";
    public double TotalWatchTimeHours { get; init; }
    public int TotalPlays { get; init; }
    public int UniqueItemsPlayed { get; init; }
    public IReadOnlyList<TopItemDto> TopItems { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopArtists { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopAlbums { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopShows { get; init; } = [];
    public IReadOnlyList<GenreStatDto> TopGenres { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopDevices { get; init; } = [];
    public IReadOnlyList<TimeSeriesPointDto> PlaysOverTime { get; init; } = [];
    public IReadOnlyList<DayOfWeekPointDto> PlaysByDayOfWeek { get; init; } = [];
    public IReadOnlyList<HourOfDayPointDto> PlaysByHourOfDay { get; init; } = [];
}

public sealed record TimeSeriesPointDto
{
    public required DateTime Date { get; init; }
    public int Count { get; init; }
}

public sealed record DayOfWeekPointDto
{
    public int Day { get; init; }
    public required string Name { get; init; }
    public int Count { get; init; }
}

public sealed record HourOfDayPointDto
{
    public int Hour { get; init; }
    public int Count { get; init; }
}
