namespace K7.Clients.Shared.Interfaces;

public interface ISharedProfileDevicePinService
{
    IReadOnlySet<Guid> GetPinnedGroupIds();

    bool IsPinned(Guid groupId);

    void SetPinned(Guid groupId, bool pinned);
}
