using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncPlayReactionPicker
{
    private bool _open;

    private static readonly string[] _emojis = K7EmojiPalette.All.ToArray();

    private async Task SendReaction(string emoji)
    {
        _open = false;
        await SyncPlay.SendReactionAsync(emoji);
    }
}
