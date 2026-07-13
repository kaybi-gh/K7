using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IHomeNavigationState
{
    HomeFocusState? SavedFocus { get; }

    void Save(Guid rowId, string mediaId, int cardIndex);
}
