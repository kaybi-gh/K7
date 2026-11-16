using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MediaShares.Commands.UpdateMediaShare;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateMediaShareCommand : IRequest
{
    public Guid Id { get; init; }
    public string? Label { get; init; }

    /// <summary>
    /// Null keeps the current password, an empty string removes it, anything else replaces it.
    /// </summary>
    public string? Password { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? MaxAccessCount { get; init; }
    public bool AllowPlayback { get; init; } = true;
    public bool AllowDownload { get; init; }
    public bool CloseWhenConsumed { get; init; }
}

public class UpdateMediaShareCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager,
    IShareAccessInvalidator invalidator)
    : IRequestHandler<UpdateMediaShareCommand>
{
    public async Task Handle(UpdateMediaShareCommand request, CancellationToken cancellationToken)
    {
        var actor = await shareManager.RequireSharingActorAsync(cancellationToken);
        var share = await shareManager.GetManagedShareAsync(request.Id, actor, cancellationToken);

        if (request.CloseWhenConsumed)
        {
            var type = await context.Medias
                .AsNoTracking()
                .Where(m => m.Id == share.RootMediaId)
                .Select(m => m.Type)
                .FirstAsync(cancellationToken);

            if (!ShareScopeResolver.IsSinglePlayableItem(type))
            {
                throw new ValidationException(
                [
                    new ValidationFailure(
                        nameof(request.CloseWhenConsumed),
                        "Only a movie, an episode or a track can close once consumed.")
                ]);
            }
        }

        share.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
        share.ExpiresAt = request.ExpiresAt?.ToUniversalTime();
        share.MaxAccessCount = request.MaxAccessCount;
        share.AllowPlayback = request.AllowPlayback;
        share.AllowDownload = request.AllowDownload;
        share.CloseWhenConsumed = request.CloseWhenConsumed;

        // Turning the option off reopens a share that was closed by a full play.
        if (!request.CloseWhenConsumed)
            share.ConsumedAt = null;

        var passwordSet = request.Password is { Length: > 0 };
        if (request.Password is not null)
        {
            share.PasswordHash = passwordSet ? PinHashHelper.Hash(request.Password) : null;
            share.FailedPasswordAttempts = 0;
            share.PasswordLockedUntil = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        // A new password must be asked from everyone, including visitors already in.
        if (passwordSet)
        {
            await context.MediaShareSessions
                .Where(s => s.ShareId == share.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        invalidator.Invalidate();
    }
}
