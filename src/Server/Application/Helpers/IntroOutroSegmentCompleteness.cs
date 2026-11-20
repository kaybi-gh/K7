using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Shared Intro/Outro completeness checks for diagnostics and incremental detection.
/// </summary>
public static class IntroOutroSegmentCompleteness
{
    /// <summary>
    /// Bump when detection can find segments it could not before, so that episodes settled as
    /// "nothing to find" by an older version are analysed and reported again.
    /// </summary>
    public const int CurrentDetectionVersion = 2;

    /// <summary>
    /// An intro shorter than this is looked for again whenever its season is analysed: it may
    /// be a fragment of a longer one. It is only replaced when detection finds another.
    /// </summary>
    public const double RetriedIntroMaxSeconds = 15;

    public const string IntroTypeName = "Intro";
    public const string OutroTypeName = "Outro";

    public static IReadOnlyList<string> GetMissingTypeNames(IReadOnlyCollection<MediaSegmentType> presentTypes)
    {
        var missing = new List<string>(2);
        if (!presentTypes.Contains(MediaSegmentType.Intro))
            missing.Add(IntroTypeName);
        if (!presentTypes.Contains(MediaSegmentType.Outro))
            missing.Add(OutroTypeName);
        return missing;
    }

    public static bool IsIncomplete(IReadOnlyCollection<MediaSegmentType> presentTypes) =>
        GetMissingTypeNames(presentTypes).Count > 0;

    /// <summary>
    /// True when the current detection already analysed the episode: what is still missing
    /// is absent from the file, not undetected.
    /// </summary>
    public static bool IsSettled(int? segmentDetectionVersion) =>
        segmentDetectionVersion >= CurrentDetectionVersion;

    /// <summary>
    /// False for a segment that detection should look for again instead of treating its type
    /// as found. See <see cref="RetriedIntroMaxSeconds"/>.
    /// </summary>
    public static bool CountsAsPresent(MediaSegmentType type, long startMs, long endMs) =>
        type != MediaSegmentType.Intro
        || endMs - startMs >= RetriedIntroMaxSeconds * 1000;

    public static bool NeedsType(IReadOnlyCollection<MediaSegmentType> presentTypes, MediaSegmentType type) =>
        !presentTypes.Contains(type);
}
