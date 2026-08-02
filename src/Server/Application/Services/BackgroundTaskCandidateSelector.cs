using System.Collections.Concurrent;
using K7.Server.Application.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

/// <summary>Minimal fields needed to decide spillover eligibility and gate acquisition.</summary>
internal readonly record struct BackgroundTaskPickCandidate(
    Guid Id,
    BackgroundTaskLane Lane,
    Guid? FederationPeerId,
    string? MetadataProviderName);

/// <summary>
/// Snapshot of which gate keys are full. Used to keep saturated work out of the candidate window
/// (WorkClass spillover) and to drive the in-process acquire loop.
/// </summary>
internal sealed class BackgroundTaskSaturationSnapshot
{
    public required HashSet<string> SaturatedKeys { get; init; }
    public required HashSet<string> SaturatedMetadataProviders { get; init; }
    public required HashSet<Guid> SaturatedFederationPeers { get; init; }
    public required HashSet<BackgroundTaskLane> SaturatedPlainLanes { get; init; }
    public required int MetadataLimit { get; init; }
    public bool MetadataCeilingHit { get; set; }
}

/// <summary>
/// Pure selection policy for background-task workers: build saturation, filter the candidate window,
/// then acquire the first eligible row (WorkClass spillover when the preferred head cannot take a slot).
/// </summary>
internal static class BackgroundTaskCandidateSelector
{
    public static BackgroundTaskSaturationSnapshot BuildSaturation(
        ConcurrentDictionary<string, int> activeCountByKey,
        Dictionary<BackgroundTaskLane, int> limits,
        IReadOnlySet<string>? coolingDownProviders = null)
    {
        var metadataLimit = limits.GetValueOrDefault(
            BackgroundTaskLane.Metadata,
            BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Metadata));

        var saturatedKeys = activeCountByKey
            .Where(kvp => kvp.Value >= BackgroundTaskConcurrencyGate.ResolveKeyLimit(kvp.Key, limits))
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.Ordinal);

        var saturatedMetadataProviders = saturatedKeys
            .Where(k => k.StartsWith(BackgroundTaskConcurrencyGate.MetadataKeyPrefix, StringComparison.Ordinal))
            .Select(k => k[BackgroundTaskConcurrencyGate.MetadataKeyPrefix.Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (coolingDownProviders is { Count: > 0 })
        {
            foreach (var provider in coolingDownProviders)
                saturatedMetadataProviders.Add(provider);
        }

        return new BackgroundTaskSaturationSnapshot
        {
            SaturatedKeys = saturatedKeys,
            SaturatedMetadataProviders = saturatedMetadataProviders,
            SaturatedFederationPeers = saturatedKeys
                .Where(k => k.StartsWith($"{BackgroundTaskLane.Federation}:", StringComparison.Ordinal))
                .Select(k => Guid.Parse(k[$"{BackgroundTaskLane.Federation}:".Length..]))
                .ToHashSet(),
            SaturatedPlainLanes = saturatedKeys
                .Where(k => !k.Contains(':', StringComparison.Ordinal))
                .Select(k => Enum.Parse<BackgroundTaskLane>(k))
                .ToHashSet(),
            MetadataLimit = metadataLimit,
            MetadataCeilingHit = metadataLimit <= 0
                || BackgroundTaskConcurrencyGate.CountMetadataActive(activeCountByKey) >= metadataLimit
        };
    }

    /// <summary>
    /// Whether a task may enter the ordered candidate window. Saturated keys are excluded so a
    /// CriticalEnrich backlog on a busy provider cannot fill the window and starve free work.
    /// </summary>
    public static bool IsEligibleForCandidateWindow(
        BackgroundTaskPickCandidate candidate,
        BackgroundTaskSaturationSnapshot saturation)
    {
        if (saturation.MetadataCeilingHit)
        {
            if (candidate.Lane == BackgroundTaskLane.Metadata)
                return false;
        }
        else if (candidate.Lane == BackgroundTaskLane.Metadata
            && saturation.SaturatedMetadataProviders.Count > 0)
        {
            var provider = string.IsNullOrWhiteSpace(candidate.MetadataProviderName)
                ? MetadataProviderNames.Local
                : candidate.MetadataProviderName;
            if (saturation.SaturatedMetadataProviders.Contains(provider))
                return false;
        }

        if (candidate.Lane == BackgroundTaskLane.Federation
            && candidate.FederationPeerId is { } peerId
            && saturation.SaturatedFederationPeers.Contains(peerId))
        {
            return false;
        }

        if (candidate.Lane is not BackgroundTaskLane.Metadata and not BackgroundTaskLane.Federation
            && saturation.SaturatedPlainLanes.Contains(candidate.Lane))
        {
            return false;
        }

        return true;
    }

    /// <summary>EF-translatable spillover filter mirroring <see cref="IsEligibleForCandidateWindow"/>.</summary>
    public static IQueryable<BackgroundTask> ApplySpilloverFilter(
        IQueryable<BackgroundTask> query,
        BackgroundTaskSaturationSnapshot saturation)
    {
        if (saturation.MetadataCeilingHit)
        {
            query = query.Where(t => t.Lane != BackgroundTaskLane.Metadata);
        }
        else if (saturation.SaturatedMetadataProviders.Count > 0)
        {
            var saturatedProviders = saturation.SaturatedMetadataProviders;
            var localProvider = MetadataProviderNames.Local;
            query = query.Where(t =>
                t.Lane != BackgroundTaskLane.Metadata
                || !saturatedProviders.Contains(t.MetadataProviderName ?? localProvider));
        }

        if (saturation.SaturatedFederationPeers.Count > 0)
        {
            var saturatedPeers = saturation.SaturatedFederationPeers;
            query = query.Where(t =>
                t.Lane != BackgroundTaskLane.Federation
                || t.FederationPeerId == null
                || !saturatedPeers.Contains(t.FederationPeerId.Value));
        }

        if (saturation.SaturatedPlainLanes.Count > 0)
        {
            var saturatedLanes = saturation.SaturatedPlainLanes;
            query = query.Where(t =>
                t.Lane == BackgroundTaskLane.Metadata
                || t.Lane == BackgroundTaskLane.Federation
                || !saturatedLanes.Contains(t.Lane));
        }

        return query;
    }

    /// <summary>
    /// Walks an already ordered candidate window and acquires the first free gate slot.
    /// Returns null when every candidate is saturated or loses the race at the gate.
    /// </summary>
    public static BackgroundTaskPickCandidate? TryAcquireNext(
        IReadOnlyList<BackgroundTaskPickCandidate> candidates,
        ConcurrentDictionary<string, int> activeCountByKey,
        Dictionary<BackgroundTaskLane, int> limits,
        BackgroundTaskSaturationSnapshot saturation,
        out string? acquiredKey)
    {
        acquiredKey = null;

        foreach (var option in candidates)
        {
            var key = BackgroundTaskConcurrencyGate.BuildKey(
                option.Lane,
                option.FederationPeerId,
                option.MetadataProviderName);

            if (saturation.SaturatedKeys.Contains(key))
                continue;

            if (option.Lane == BackgroundTaskLane.Metadata && saturation.MetadataCeilingHit)
                continue;

            var keyLimit = BackgroundTaskConcurrencyGate.ResolveKeyLimit(key, limits);
            if (!BackgroundTaskConcurrencyGate.TryAcquire(activeCountByKey, key, keyLimit, limits))
            {
                saturation.SaturatedKeys.Add(key);
                if (BackgroundTaskConcurrencyGate.IsMetadataKey(key)
                    && BackgroundTaskConcurrencyGate.CountMetadataActive(activeCountByKey) >= saturation.MetadataLimit)
                {
                    saturation.MetadataCeilingHit = true;
                }

                continue;
            }

            acquiredKey = key;
            return option;
        }

        return null;
    }
}
