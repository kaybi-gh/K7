namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMovieDto : LiteMediaDto
{
    public string? Toto { get; init; } = "test2";
}
