using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncPlayChat : IDisposable
{
    [Parameter] public bool Inline { get; set; }

    private string _inputText = "";
    private ElementReference _messagesRef;

    protected override void OnInitialized()
    {
        SyncPlay.ChatMessageReceived += OnChatMessageReceived;
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_inputText)) return;

        await SyncPlay.SendChatAsync(_inputText.Trim());
        _inputText = "";
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SendMessage();
        }
    }

    private void OnChatMessageReceived(SyncPlayChatMessageDto _) => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        SyncPlay.ChatMessageReceived -= OnChatMessageReceived;
    }
}
