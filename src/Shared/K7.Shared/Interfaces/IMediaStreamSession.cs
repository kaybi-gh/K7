using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IMediaStreamSession
{
    Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings);
    Task SendPlaybackState(Guid streamId, PlaybackState state, double position);
    Task ReceiveIndexedFileStreamUri(IndexedFileStreamUri streamUri);
}

