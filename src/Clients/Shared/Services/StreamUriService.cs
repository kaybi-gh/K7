using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class StreamUriService : IStreamUriService
{
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;

    public StreamUriService(IK7ServerService k7ServerService, IDeviceStorageService deviceStorageService)
    {
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
    }

    public async Task<StreamingSessionDto> GetOrCreateSessionAsync(Guid indexedFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
    {
        var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

        if (!string.IsNullOrWhiteSpace(storedDeviceId))
        {
            var request = new CreateStreamSessionRequest
            {
                IndexedFileId = indexedFileId,
                DeviceId = Guid.Parse(storedDeviceId),
                AudioTrackIndex = audioTrackIndex
            };

            var session = await _k7ServerService.CreateStreamSessionAsync(request, cancellationToken)
                         ?? throw new Exception($"No streaming session created for IndexedFile with id '{indexedFileId}'.");

            if (session.Source is not null)
            {
                session.Source.Uri = _k7ServerService.GetAbsoluteUri(session.Source.Uri.OriginalString)!;
            }

            return session;
        }

        throw new InvalidOperationException($"Missing {nameof(PreferenceKeys.DEVICE_ID)}");
    }
}
