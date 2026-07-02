namespace K7.Shared.Dtos;

public sealed record SharedProfilePreferencesDto
{
    public bool BlockNewMembership { get; set; } = true;
}
