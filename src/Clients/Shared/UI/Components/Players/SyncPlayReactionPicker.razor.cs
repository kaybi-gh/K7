using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncPlayReactionPicker
{
    private bool _open;

    private static readonly string[] _emojis =
    [
        "\U0001F44D", // thumbs up
        "\U0001F602", // face with tears of joy
        "\U0001F525", // fire
        "\u2764\uFE0F", // red heart
        "\U0001F62E", // face with open mouth
        "\U0001F622", // crying face
        "\U0001F44F", // clapping hands
        "\U0001F389"  // party popper
    ];

    private async Task SendReaction(string emoji)
    {
        _open = false;
        await SyncPlay.SendReactionAsync(emoji);
    }
}
