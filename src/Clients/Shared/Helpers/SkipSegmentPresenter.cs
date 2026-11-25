using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Shared intro/outro skip state machine for Blazor <c>SkipSegmentOverlay</c> and MAUI
/// <c>NativeVideoPlayerOverlay</c>.
/// Show-button: floating offer without chrome, Enter skips, auto-hide after
/// <see cref="DisplayDuration"/>, then the same button stays available while chrome is visible
/// for the rest of the chapter. Auto-skip seeks to the segment end (or ends playback when
/// the outro reaches the file end). Disabled does nothing.
/// After a media change the engine can keep reporting the previous item's playhead.
/// <see cref="HoldForMediaChange"/> hides skip offers until that clock has left the old
/// position for two samples, so a stale outro cannot end the successor episode.
/// </summary>
public static class SkipSegmentPresenter
{
    public static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How far the playhead must move from the previous item before a skip offer can
    /// belong to the new one. A few seconds of jitter around the old outro must not count.
    /// </summary>
    public const double MediaChangeClockGapSeconds = 15;

    /// <summary>
    /// A position this close to the start counts as having left the previous item.
    /// Source swaps also publish a synthetic 0, so one such sample is not confirmation.
    /// </summary>
    public const double MediaChangeStartSeconds = 2;

    /// <summary>
    /// What may be left after an outro for skipping it to still end the episode. Detected credits
    /// often stop a few seconds short of the file (studio logos, a black tail), and playing those
    /// out after a skip delays the next-episode offer for nothing.
    /// </summary>
    public const double OutroEndToleranceSeconds = 5.0;

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
        bool ChromeVisible,
        DateTime ShowTimeUtc,
        DateTime LastSkipUtc,
        bool ClockHeld,
        double ClockHoldAnchorSeconds,
        bool ClockHoldPending);

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

            // Exclusive end so a seek to EndMs leaves the window and does not reshow skip.
            if (currentMs >= segment.StartMs && currentMs < segment.EndMs)
                return segment;
        }

        return null;
    }

    /// <summary>
    /// Skipping an outro that runs to (or within <see cref="OutroEndToleranceSeconds"/> of)
    /// duration must end the episode so the next-episode offer runs. Seeking to EndMs parks on
    /// the last frame, looks paused, and a second skip can then fire Ended on the successor episode.
    /// </summary>
    public static bool CompletesPlayback(MediaSegmentDto segment, double durationSeconds)
    {
        return segment.Type == MediaSegmentType.Outro
            && NativeVideoPlaybackEnd.IsAtMediaEnd(segment.EndMs / 1000.0, durationSeconds, OutroEndToleranceSeconds);
    }

    /// <summary>
    /// Call when the playing media id changes, with the last playhead accepted for the
    /// previous item. A position still near the start does not need a hold.
    /// </summary>
    public static State HoldForMediaChange(double previousTimeSeconds)
    {
        if (previousTimeSeconds <= MediaChangeStartSeconds)
            return default;

        return new State
        {
            ClockHeld = true,
            ClockHoldAnchorSeconds = previousTimeSeconds
        };
    }

    public static Result Tick(
        State previous,
        IReadOnlyList<MediaSegmentDto>? segments,
        VideoPlayerSettingsDto? settings,
        double timeSeconds,
        bool chromeVisible,
        DateTime utcNow)
    {
        if (previous.ClockHoldAnchorSeconds > MediaChangeStartSeconds)
        {
            var away = IsClockAwayFromPreviousItem(previous.ClockHoldAnchorSeconds, timeSeconds);
            if (!away)
            {
                // Still the previous item, or the engine bounced back to it after a
                // synthetic 0. Do not arm skip on the successor.
                return new Result(
                    previous with
                    {
                        ActiveSegment = null,
                        Visible = false,
                        ClockHeld = true,
                        ClockHoldPending = false,
                        ChromeVisible = chromeVisible
                    },
                    ActionKind.None);
            }

            if (!previous.ClockHoldPending)
            {
                return new Result(
                    previous with
                    {
                        ActiveSegment = null,
                        Visible = false,
                        ClockHeld = true,
                        ClockHoldPending = true,
                        ChromeVisible = chromeVisible
                    },
                    ActionKind.None);
            }

            if (previous.ClockHeld)
            {
                // Second sample on the new item. Allow skip, but remember the anchor
                // so one late stale position can put the hold back.
                previous = default(State) with
                {
                    ChromeVisible = chromeVisible,
                    ClockHoldAnchorSeconds = previous.ClockHoldAnchorSeconds,
                    ClockHoldPending = true
                };
            }
            else if (segments is not null && segments.Count > 0 && settings is not null)
            {
                // Forget the previous item only once this sample can be matched
                // against the new list. Clearing earlier lets a stale Apply use the
                // old outro against the successor.
                previous = previous with
                {
                    ClockHoldAnchorSeconds = 0,
                    ClockHoldPending = false
                };
            }
        }

        if (segments is null || segments.Count == 0 || settings is null)
        {
            return new Result(
                previous with { ActiveSegment = null, Visible = false, ChromeVisible = chromeVisible },
                ActionKind.None);
        }

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
                    Dismissed = dismissed,
                    ChromeVisible = chromeVisible
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
                    Dismissed = dismissed,
                    ChromeVisible = chromeVisible
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
                        ChromeVisible = chromeVisible,
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
                    Dismissed = dismissed,
                    ChromeVisible = chromeVisible
                },
                ActionKind.None);
        }

        var visible = previous.Visible;
        if (inCooldown)
        {
            visible = false;
        }
        else if (!dismissed || chromeVisible)
        {
            if (!visible)
            {
                showTimeUtc = utcNow;
                visible = true;
            }
            else if (!chromeVisible)
            {
                // Chrome-visible time must not eat the floating 5s window. When chrome
                // hides, restart DisplayDuration so skip stays on screen after controls.
                if (previous.ChromeVisible)
                    showTimeUtc = utcNow;

                if (utcNow - showTimeUtc >= DisplayDuration)
                {
                    visible = false;
                    dismissed = true;
                }
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
                ChromeVisible = chromeVisible,
                ShowTimeUtc = showTimeUtc
            },
            ActionKind.None);
    }

    private static bool IsClockAwayFromPreviousItem(double anchorSeconds, double timeSeconds) =>
        timeSeconds <= MediaChangeStartSeconds
        || Math.Abs(timeSeconds - anchorSeconds) >= MediaChangeClockGapSeconds;
}
