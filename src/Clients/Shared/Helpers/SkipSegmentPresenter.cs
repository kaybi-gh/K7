using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Shared intro/outro skip state machine for Blazor <c>SkipSegmentOverlay</c> and MAUI
/// <c>NativeVideoPlayerOverlay</c>.
/// Show-button: floating offer without chrome, Enter skips, auto-hide after
/// <see cref="DisplayDuration"/>, then the same button stays available while chrome is visible
/// for the rest of the chapter. Auto-skip seeks to the segment end. Disabled does nothing.
/// </summary>
public static class SkipSegmentPresenter
{
    public static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    public enum ActionKind
    {
        None,
        AutoSkip
    }

    public readonly record struct State(
        MediaSegmentDto? ActiveSegment,
        bool Visible,
        bool AutoSkipped,
        bool Dismissed,
        DateTime ShowTimeUtc,
        DateTime LastSkipUtc);

    public readonly record struct Result(State State, ActionKind Action);

    public static MediaSegmentDto? FindActive(IReadOnlyList<MediaSegmentDto>? segments, double timeSeconds)
    {
        if (segments is null || segments.Count == 0)
            return null;

        var currentMs = (long)(timeSeconds * 1000.0);
        foreach (var segment in segments)
        {
            if (segment.Type is not (MediaSegmentType.Intro or MediaSegmentType.Outro))
                continue;

            if (currentMs >= segment.StartMs && currentMs <= segment.EndMs)
                return segment;
        }

        return null;
    }

    public static Result Tick(
        State previous,
        IReadOnlyList<MediaSegmentDto>? segments,
        VideoPlayerSettingsDto? settings,
        double timeSeconds,
        bool chromeVisible,
        DateTime utcNow)
    {
        if (segments is null || segments.Count == 0 || settings is null)
            return new Result(previous with { ActiveSegment = null, Visible = false }, ActionKind.None);

        var active = FindActive(segments, timeSeconds);
        var autoSkipped = previous.AutoSkipped;
        var dismissed = previous.Dismissed;
        var showTimeUtc = previous.ShowTimeUtc;

        if (active != previous.ActiveSegment)
        {
            autoSkipped = false;
            dismissed = false;
        }

        if (active is null)
        {
            return new Result(
                previous with
                {
                    ActiveSegment = null,
                    Visible = false,
                    AutoSkipped = autoSkipped,
                    Dismissed = dismissed
                },
                ActionKind.None);
        }

        var behavior = active.Type == MediaSegmentType.Intro
            ? settings.IntroSkipBehavior
            : settings.OutroSkipBehavior;

        if (behavior == IntroSkipBehavior.Disabled)
        {
            return new Result(
                previous with
                {
                    ActiveSegment = active,
                    Visible = false,
                    AutoSkipped = autoSkipped,
                    Dismissed = dismissed
                },
                ActionKind.None);
        }

        var inCooldown = utcNow - previous.LastSkipUtc < Cooldown;

        if (behavior == IntroSkipBehavior.AutoSkip)
        {
            if (!autoSkipped && !inCooldown)
            {
                return new Result(
                    previous with
                    {
                        ActiveSegment = active,
                        Visible = false,
                        AutoSkipped = true,
                        Dismissed = dismissed,
                        LastSkipUtc = utcNow
                    },
                    ActionKind.AutoSkip);
            }

            return new Result(
                previous with
                {
                    ActiveSegment = active,
                    Visible = false,
                    AutoSkipped = autoSkipped,
                    Dismissed = dismissed
                },
                ActionKind.None);
        }

        var visible = previous.Visible;
        if (!dismissed || chromeVisible)
        {
            if (!visible)
            {
                showTimeUtc = utcNow;
                visible = true;
            }
            else if (!chromeVisible && utcNow - showTimeUtc >= DisplayDuration)
            {
                visible = false;
                dismissed = true;
            }
        }
        else
        {
            visible = false;
        }

        return new Result(
            previous with
            {
                ActiveSegment = active,
                Visible = visible,
                AutoSkipped = autoSkipped,
                Dismissed = dismissed,
                ShowTimeUtc = showTimeUtc
            },
            ActionKind.None);
    }
}
