using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// In-memory grid slot used while a library browse page is still on the wire.
/// Rendered as a focusable empty tile so D-pad can walk past loaded MediaCards.
/// Replaced in place when the page arrives (same cell key).
/// </summary>
public sealed record UnloadedBrowseItem : LiteMediaDto
{
    public int SlotIndex { get; init; }

    public static Guid IdFor(int slotIndex)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), slotIndex);
        bytes[15] = 0x0B;
        return new Guid(bytes);
    }
}
