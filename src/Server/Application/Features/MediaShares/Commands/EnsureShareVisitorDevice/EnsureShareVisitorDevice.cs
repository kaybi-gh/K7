using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.MediaShares.Commands.EnsureShareVisitorDevice;

/// <summary>
/// Checks that the visitor may play a file through a share, and returns the technical
/// device that carries its browser capabilities. Stream negotiation needs a device:
/// visitors have none of their own, so each visitor session gets one.
/// </summary>
public record EnsureShareVisitorDeviceCommand : IRequest<Guid>
{
    public required Guid ShareId { get; init; }
    public required Guid IndexedFileId { get; init; }
    public IReadOnlyList<string> SupportedContainers { get; init; } = [];
    public IReadOnlyList<string> SupportedAudioCodecs { get; init; } = [];
    public IReadOnlyList<string> SupportedVideoCodecs { get; init; } = [];
    public IReadOnlyList<string> CapabilityTokens { get; init; } = [];
    public bool SupportsHdr { get; init; }
    public double DisplayResolutionWidth { get; init; }
    public double DisplayResolutionHeight { get; init; }
}

public class EnsureShareVisitorDeviceCommandHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<EnsureShareVisitorDeviceCommand, Guid>
{
    public const string DeviceName = "Share visitor";
    private const int MaxTokens = 128;

    public async Task<Guid> Handle(EnsureShareVisitorDeviceCommand request, CancellationToken cancellationToken)
    {
        var grant = shareVisitorAccess.RequireGrant(request.ShareId);
        await accessGuard.EnsureAccessByIndexedFileAsync(request.IndexedFileId, cancellationToken);

        var matches = await shareVisitorAccess.GetMatchingSharesByIndexedFileAsync(request.IndexedFileId, cancellationToken);
        var match = matches.FirstOrDefault(m => m.ShareId == request.ShareId);
        if (match is null)
            throw new NotFoundException(request.IndexedFileId.ToString(), "IndexedFile");

        if (!match.AllowPlayback)
            throw new ForbiddenAccessException();

        var session = await context.MediaShareSessions
            .Include(s => s.Device)
            .FirstOrDefaultAsync(s => s.Id == grant.SessionId, cancellationToken);

        if (session is null)
            throw new NotFoundException(request.ShareId.ToString(), nameof(MediaShare));

        var formatIds = ResolveFormatIds(request);

        var device = session.Device;
        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceName = DeviceName,
                ClientType = ClientType.Web,
                DeviceType = DeviceType.Unknown
            };
            context.Devices.Add(device);
            session.Device = device;
        }

        device.PlaybackCapabilities = new DevicePlaybackCapabilities
        {
            SupportedMediaFormatIds = formatIds,
            SupportedSubtitlesCodecs = ["webvtt"],
            SupportsHDR = request.SupportsHdr
        };
        device.DisplayResolutionWidth = Math.Max(0, request.DisplayResolutionWidth);
        device.DisplayResolutionHeight = Math.Max(0, request.DisplayResolutionHeight);
        device.DisplayScreenWidth = device.DisplayResolutionWidth;
        device.DisplayScreenHeight = device.DisplayResolutionHeight;
        device.LastSeen = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return device.Id;
    }

    /// <summary>
    /// Same matching a web client does before registering its device: a catalog format is
    /// supported when the browser plays its container and its codecs.
    /// </summary>
    internal static List<string> ResolveFormatIds(EnsureShareVisitorDeviceCommand request)
    {
        var containers = ToSet(request.SupportedContainers);
        var audioCodecs = ToSet(request.SupportedAudioCodecs);
        var videoCodecs = ToSet(request.SupportedVideoCodecs);

        var ids = Constants.MediaFormats
            .Where(format => format switch
            {
                AudioMediaFormat audio =>
                    containers.Contains(audio.Container) && audioCodecs.Contains(audio.Codec),
                VideoMediaFormat video =>
                    containers.Contains(video.Container)
                    && videoCodecs.Contains(video.VideoCodec)
                    && (string.IsNullOrEmpty(video.AudioCodec) || audioCodecs.Contains(video.AudioCodec)),
                _ => false
            })
            .Select(format => format.Id)
            .ToList();

        ids.AddRange(ToSet(request.CapabilityTokens));
        return ids;
    }

    private static HashSet<string> ToSet(IReadOnlyList<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 128)
            .Take(MaxTokens)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
