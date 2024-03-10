using MediaServer.Domain.Entities.Users;

namespace MediaServer.Domain.Entities.Ratings;
public class UserRating : BaseRating
{
    public UserRating() : base(RatingSource.LocalUser) { }

    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;
}
