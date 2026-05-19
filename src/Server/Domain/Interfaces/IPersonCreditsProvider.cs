namespace K7.Server.Domain.Interfaces;

public interface IPersonCreditsProvider
{
    Task<IReadOnlyList<ExternalPersonCredit>> GetPersonCreditsAsync(string providerId, CancellationToken cancellationToken = default);
}

public record ExternalPersonCredit
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public int? Year { get; init; }
    public required string MediaType { get; init; }
    public string? PosterPath { get; init; }
    public double Popularity { get; init; }
}
