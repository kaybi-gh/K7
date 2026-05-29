using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncPlayReactionOverlay : IDisposable
{
    private static readonly Random _random = new();
    private readonly List<ActiveReaction> _activeReactions = [];

    protected override void OnInitialized()
    {
        SyncPlay.ReactionReceived += OnReactionReceived;
    }

    private void OnReactionReceived(SyncPlayReactionDto reaction)
    {
        var active = new ActiveReaction
        {
            Id = Guid.NewGuid(),
            Emoji = reaction.Emoji,
            DisplayName = reaction.DisplayName,
            X = _random.Next(10, 80)
        };

        _activeReactions.Add(active);

        _ = InvokeAsync(async () =>
        {
            StateHasChanged();
            await Task.Delay(2500);
            active.Fading = true;
            StateHasChanged();
            await Task.Delay(500);
            _activeReactions.Remove(active);
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        SyncPlay.ReactionReceived -= OnReactionReceived;
    }

    private sealed class ActiveReaction
    {
        public Guid Id { get; init; }
        public string Emoji { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int X { get; init; }
        public bool Fading { get; set; }
    }
}
