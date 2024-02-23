using MediaServer.Domain.Entities.Users;

namespace MediaServer.Domain.Entities.Ratings;
public class UserRating : BaseRating
{
    public UserRating() : base(RatingSource.Local) { }

    public required Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
