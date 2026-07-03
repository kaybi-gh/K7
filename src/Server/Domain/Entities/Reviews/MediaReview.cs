using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Entities.Reviews;

public class MediaReview : BaseAuditableEntity
{
    public required Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public required Guid UserRatingId { get; set; }
    public UserRating UserRating { get; set; } = null!;

    public required string Text { get; set; }
    public string? Emoji { get; set; }
}
