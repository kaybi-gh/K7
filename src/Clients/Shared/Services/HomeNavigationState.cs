using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class HomeNavigationState : IHomeNavigationState
{
    public HomeFocusState? SavedFocus { get; private set; }

    public void Save(Guid rowId, string mediaId, int cardIndex) =>
        SavedFocus = new HomeFocusState(rowId, mediaId, cardIndex);
}
