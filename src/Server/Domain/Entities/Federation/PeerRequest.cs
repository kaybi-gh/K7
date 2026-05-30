namespace K7.Server.Domain.Entities.Federation;

public class PeerRequest : BaseAuditableEntity
{
    public required string RequesterUrl { get; set; }
    public required string RequesterName { get; set; }
    public PeerRequestStatus Status { get; set; } = PeerRequestStatus.Pending;
    public required string Token { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
