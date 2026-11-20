using System.Text.RegularExpressions;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Maps named file chapters to Intro/Outro when chromaprint finds nothing.
/// </summary>
/// <remarks>
/// A chapter title is an explicit statement by whoever authored the file, so its bounds are
/// looser than the audio matcher's: a 6 second title card is a real intro when the file says so,
/// and an anime opening may sit well past the first quarter of the episode.
/// </remarks>
public static partial class ChapterSegmentFallbackHelper
{
    public const double MinIntroDurationSeconds = 5;
    public const double MaxIntroDurationSeconds = 180;
    public const double MinOutroDurationSeconds = 5;
    public const double MaxOutroDurationSeconds = 450;

    /// <summary>Openings must start in the first half of the episode, endings in the second half.</summary>
    private const double HalfwayShare = 0.5;

    public static bool TryCreateSegment(
        ChapterMarker chapter,
        double episodeDurationSeconds,
        MediaSegmentType type,
        out double startSeconds,
        out double endSeconds)
    {
        startSeconds = 0;
        endSeconds = 0;

        if (string.IsNullOrWhiteSpace(chapter.Title) || episodeDurationSeconds <= 0)
            return false;

        // "Post Intro", "Skip Intro", "Post-Credits", "Mid-Credits Scene": markers around the
        // segment, or story that follows it, never the segment itself.
        if (AdjacentMarkerRegex().IsMatch(chapter.Title))
            return false;

        if (type == MediaSegmentType.Intro && !IntroTitleRegex().IsMatch(chapter.Title))
            return false;
        if (type == MediaSegmentType.Outro && !OutroTitleRegex().IsMatch(chapter.Title))
            return false;

        var halfway = episodeDurationSeconds * HalfwayShare;
        if (type == MediaSegmentType.Intro && chapter.StartSeconds > halfway)
            return false;
        if (type == MediaSegmentType.Outro && chapter.StartSeconds < halfway)
            return false;

        startSeconds = Math.Max(0, chapter.StartSeconds);
        endSeconds = chapter.EndSeconds ?? InferEndSeconds(chapter, episodeDurationSeconds, type);
        endSeconds = Math.Min(endSeconds, episodeDurationSeconds);
        if (endSeconds <= startSeconds)
            return false;

        var duration = endSeconds - startSeconds;
        var (min, max) = type == MediaSegmentType.Intro
            ? (MinIntroDurationSeconds, MaxIntroDurationSeconds)
            : (MinOutroDurationSeconds, MaxOutroDurationSeconds);

        return duration >= min && duration <= max;
    }

    private static double InferEndSeconds(
        ChapterMarker chapter,
        double episodeDurationSeconds,
        MediaSegmentType type)
    {
        // Untitled end: assume a typical length capped by episode remaining time.
        var defaultLength = type == MediaSegmentType.Intro ? 90.0 : 60.0;
        return Math.Min(episodeDurationSeconds, chapter.StartSeconds + defaultLength);
    }

    [GeneratedRegex(
        @"\b(opening(?!\s*credits?\s*end)|intro(duction)?|op\d*|(main\s*)?title\s*sequence|main\s*titles?|vorspann|abertura|g[eé]n[eé]rique(?!\s*(de\s*)?(fin|end|closing)))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntroTitleRegex();

    [GeneratedRegex(
        @"\b(ending|outro|ed\d*|(?<!opening\s)credits?|closing|end\s*titles?|abspann|g[eé]n[eé]rique(?!\s*(de\s*)?d[eé]but))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutroTitleRegex();

    [GeneratedRegex(
        @"\b(post|mid|after|pre|skip)[\s-]*(intro|opening|credits?|ending|outro)\b|\b(stinger|credits?\s*scene|credits?\s*end)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdjacentMarkerRegex();
}
