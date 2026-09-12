namespace K7.Shared.Dtos.Restrictions;

public sealed record UpdateAgeRestrictionRequest
{
    public required bool Enabled { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public bool HideUnratedTitles { get; init; } = true;
}
