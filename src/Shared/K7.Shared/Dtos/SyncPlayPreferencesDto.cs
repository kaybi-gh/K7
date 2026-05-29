namespace K7.Shared.Dtos;

public sealed record SyncPlayPreferencesDto
{
    public bool InvitationsEnabled { get; set; } = true;
}
