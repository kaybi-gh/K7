using K7.Shared.Dtos.Home;

namespace K7.Clients.Shared.Models;

public sealed class HomeFeedRow
{
    public required HomeRowConfigDto Config { get; init; }

    public List<MediaCardViewModel> Items { get; } = [];
}
