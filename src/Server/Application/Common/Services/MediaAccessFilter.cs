using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

public sealed record ContentAccessGates(
    ContentRestrictionProfile? RestrictionProfile,
    AgeRestrictionGate? AgeGate);

/// <summary>
/// Centralizes the per-user media visibility filtering shared by the media list, search and home feed
/// queries: library exclusions, media exclusions and content restriction profiles. Keeping this logic
/// in one place avoids the predicates drifting apart between endpoints.
/// </summary>
public sealed class MediaAccessFilter(IApplicationDbContext context)
{
    /// <summary>
    /// Hides media mirrored from a peer that is not Active or whose last connectivity test failed.
    /// Local-origin media (<c>PeerServerId == null</c>) is always kept.
    /// </summary>
    public static IQueryable<BaseMedia> ExcludeUnavailablePeers(
        IApplicationDbContext context,
        IQueryable<BaseMedia> query)
    {
        var unavailablePeerIds = context.PeerServers
            .Where(p => p.Status != PeerStatus.Active || p.LastTestSucceeded == false)
            .Select(p => p.Id);

        return query.Where(m => m.PeerServerId == null || !unavailablePeerIds.Contains(m.PeerServerId.Value));
    }

    public IQueryable<BaseMedia> ApplyUnavailablePeerExclusion(IQueryable<BaseMedia> query) =>
        ExcludeUnavailablePeers(context, query);

    /// <summary>
    /// Applies the library-level and media-level exclusions for the given user. Does not apply the
    /// content restriction profile (see <see cref="ApplyAllAsync"/> or <see cref="GetRestrictionProfileAsync"/>).
    /// </summary>
    public IQueryable<BaseMedia> ApplyExclusions(IQueryable<BaseMedia> query, Guid userId)
    {
        query = ApplyUnavailablePeerExclusion(query);

        var excludedLibraryIds = context.UserLibraryExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId);

        query = query.WhereAvailableOutsideExcludedLibraries(context, excludedLibraryIds);

        var excludedMediaIds = context.UserMediaExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId);

        return query.WhereNotUserExcluded(excludedMediaIds);
    }

    public IQueryable<Guid> GetAccessibleMediaIds(Guid userId) =>
        ApplyExclusions(context.Medias, userId).Select(m => m.Id);

    public Task<ContentRestrictionProfile?> GetRestrictionProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetRestrictionProfileAsync(userId, sharedProfileId: null, cancellationToken);

    /// <summary>
    /// Resolves the restriction profile for the current session: the shared profile's assigned
    /// profile when a shared session is active (including none), otherwise the user's personal one.
    /// </summary>
    public async Task<ContentRestrictionProfile?> GetRestrictionProfileAsync(
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default) =>
        (await GetContentGatesAsync(userId, sharedProfileId, cancellationToken)).RestrictionProfile;

    public Task<AgeRestrictionGate?> GetAgeRestrictionGateAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetAgeRestrictionGateAsync(userId, sharedProfileId: null, cancellationToken);

    public async Task<AgeRestrictionGate?> GetAgeRestrictionGateAsync(
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default) =>
        (await GetContentGatesAsync(userId, sharedProfileId, cancellationToken)).AgeGate;

    /// <summary>
    /// Loads the custom restriction profile and age gate in one round trip.
    /// Shared sessions use the shared profile settings, never the acting member's.
    /// </summary>
    public async Task<ContentAccessGates> GetContentGatesAsync(
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        if (sharedProfileId is { } profileId)
        {
            var shared = await context.SharedProfiles
                .AsNoTracking()
                .Where(p => p.Id == profileId)
                .Select(p => new
                {
                    p.ContentRestrictionProfile,
                    p.AgeRestrictionEnabled,
                    p.ViewerDateOfBirth,
                    p.HideUnratedTitles
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (shared is null)
                return new ContentAccessGates(null, null);

            var sharedGate = AgeRestrictionEvaluator.IsActive(shared.AgeRestrictionEnabled, shared.ViewerDateOfBirth)
                ? new AgeRestrictionGate(shared.ViewerDateOfBirth!.Value, shared.HideUnratedTitles)
                : null;
            return new ContentAccessGates(shared.ContentRestrictionProfile, sharedGate);
        }

        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.ContentRestrictionProfile,
                u.AgeRestrictionEnabled,
                u.DateOfBirth,
                u.HideUnratedTitles
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return new ContentAccessGates(null, null);

        var userGate = AgeRestrictionEvaluator.IsActive(user.AgeRestrictionEnabled, user.DateOfBirth)
            ? new AgeRestrictionGate(user.DateOfBirth!.Value, user.HideUnratedTitles)
            : null;
        return new ContentAccessGates(user.ContentRestrictionProfile, userGate);
    }

    public async Task<bool> HasActiveContentGateAsync(
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        var gates = await GetContentGatesAsync(userId, sharedProfileId, cancellationToken);
        return gates.RestrictionProfile is not null || gates.AgeGate is not null;
    }

    public IQueryable<BaseMedia> ApplyAgeRestriction(
        IQueryable<BaseMedia> query,
        AgeRestrictionGate gate) =>
        AgeRestrictionEvaluator.Apply(
            query,
            gate.DateOfBirth,
            DateOnly.FromDateTime(DateTime.UtcNow),
            gate.HideUnratedTitles);

    /// <summary>
    /// Applies exclusions, the content restriction profile, and age restriction in one pass.
    /// </summary>
    public Task<IQueryable<BaseMedia>> ApplyAllAsync(
        IQueryable<BaseMedia> query,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ApplyAllAsync(query, userId, sharedProfileId: null, cancellationToken);

    public async Task<IQueryable<BaseMedia>> ApplyAllAsync(
        IQueryable<BaseMedia> query,
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        query = ApplyExclusions(query, userId);

        var gates = await GetContentGatesAsync(userId, sharedProfileId, cancellationToken);
        if (gates.RestrictionProfile is not null)
            query = ContentRestrictionEvaluator.ApplyRestriction(query, gates.RestrictionProfile);

        if (gates.AgeGate is not null)
            query = ApplyAgeRestriction(query, gates.AgeGate);

        return query;
    }
}
