using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Shares;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Commands.CreateMediaShare;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateMediaShareCommand : IRequest<MediaShareLinkDto>
{
    public required Guid MediaId { get; init; }
    public string? Label { get; init; }
    public string? Password { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? MaxAccessCount { get; init; }
    public bool AllowPlayback { get; init; } = true;
    public bool AllowDownload { get; init; }
    public bool CloseWhenConsumed { get; init; }
}

public class CreateMediaShareCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<CreateMediaShareCommand, MediaShareLinkDto>
{
    public async Task<MediaShareLinkDto> Handle(CreateMediaShareCommand request, CancellationToken cancellationToken)
    {
        await shareManager.EnsureEnabledAsync(cancellationToken);
        var actor = await shareManager.RequireSharingActorAsync(cancellationToken);

        // Nobody shares what they cannot see themselves.
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var media = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == request.MediaId)
            .Select(m => new { m.Type, m.PeerServerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (media is null)
            throw new NotFoundException(request.MediaId.ToString(), nameof(BaseMedia));

        if (media.PeerServerId is not null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(request.MediaId), "Media hosted on another server cannot be shared.")
            ]);
        }

        if (request.CloseWhenConsumed && !ShareScopeResolver.IsSinglePlayableItem(media.Type))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(request.CloseWhenConsumed),
                    "Only a movie, an episode or a track can close once consumed.")
            ]);
        }

        var token = ShareTokenHelper.Generate();
        var share = new MediaShare
        {
            Id = Guid.NewGuid(),
            RootMediaId = request.MediaId,
            TokenHash = ShareTokenHelper.Hash(token),
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
            PasswordHash = string.IsNullOrEmpty(request.Password) ? null : PinHashHelper.Hash(request.Password),
            ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
            MaxAccessCount = request.MaxAccessCount,
            AllowPlayback = request.AllowPlayback,
            AllowDownload = request.AllowDownload,
            CloseWhenConsumed = request.CloseWhenConsumed,
            CreatedByUserId = actor.UserId
        };

        context.MediaShares.Add(share);
        await context.SaveChangesAsync(cancellationToken);

        return new MediaShareLinkDto { Id = share.Id, Token = token };
    }
}
