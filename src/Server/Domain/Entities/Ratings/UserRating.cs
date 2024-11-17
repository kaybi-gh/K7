using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Entities.Ratings;
public class UserRating : BaseRating
{
    public UserRating() : base(RatingSource.LocalUser) { }

    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;
}
