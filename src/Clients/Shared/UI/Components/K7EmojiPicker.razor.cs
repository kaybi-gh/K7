using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7EmojiPicker
{
    [Parameter] public string? SelectedEmoji { get; set; }
    [Parameter] public EventCallback<string?> SelectedEmojiChanged { get; set; }
    [Parameter] public bool ShowToggle { get; set; } = true;
    [Parameter] public bool AllowClear { get; set; } = true;

    private bool _open;

    private async Task SelectEmoji(string emoji)
    {
        _open = false;
        SelectedEmoji = emoji;
        await SelectedEmojiChanged.InvokeAsync(emoji);
    }

    private async Task ClearEmoji()
    {
        _open = false;
        SelectedEmoji = null;
        await SelectedEmojiChanged.InvokeAsync(null);
    }
}
