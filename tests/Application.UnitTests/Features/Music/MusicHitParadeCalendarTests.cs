using K7.Shared;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.Music;

[TestFixture]
public class MusicHitParadeCalendarTests
{
    [Test]
    public void GetWindow_ShouldReturnNull_WhenPeriodIsAll()
    {
        MusicHitParadeCalendar.GetWindow(MusicHitParadePeriods.All, 2026).Should().BeNull();
    }

    [Test]
    public void GetWindow_ShouldUseExclusiveEnd_ForCalendarYear()
    {
        var window = MusicHitParadeCalendar.GetWindow(MusicHitParadePeriods.Year, 2026, timeZone: TimeZoneInfo.Utc);

        window.Should().NotBeNull();
        window!.Value.From.UtcDateTime.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        window.Value.To.UtcDateTime.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void GetWindow_ShouldSpanPreviousDecember_ForWinter()
    {
        var window = MusicHitParadeCalendar.GetWindow(
            MusicHitParadePeriods.Season,
            2026,
            season: MusicHitParadeSeasons.Winter,
            timeZone: TimeZoneInfo.Utc);

        window.Should().NotBeNull();
        window!.Value.From.UtcDateTime.Should().Be(new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        window.Value.To.UtcDateTime.Should().Be(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void GetWindow_ShouldUseMarchToJune_ForSpring()
    {
        var window = MusicHitParadeCalendar.GetWindow(
            MusicHitParadePeriods.Season,
            2026,
            season: MusicHitParadeSeasons.Spring,
            timeZone: TimeZoneInfo.Utc);

        window.Should().NotBeNull();
        window!.Value.From.UtcDateTime.Should().Be(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        window.Value.To.UtcDateTime.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ShiftSeason_ShouldWrapToPreviousYear()
    {
        var (year, season) = MusicHitParadeCalendar.ShiftSeason(2026, MusicHitParadeSeasons.Winter, -1);

        year.Should().Be(2025);
        season.Should().Be(MusicHitParadeSeasons.Autumn);
    }

    [Test]
    public void ShiftSeason_ShouldWrapToNextYear()
    {
        var (year, season) = MusicHitParadeCalendar.ShiftSeason(2026, MusicHitParadeSeasons.Autumn, 1);

        year.Should().Be(2027);
        season.Should().Be(MusicHitParadeSeasons.Winter);
    }

    [Test]
    public void ShiftMonth_ShouldWrapToNextYear()
    {
        var (year, month) = MusicHitParadeCalendar.ShiftMonth(2026, 12, 1);

        year.Should().Be(2027);
        month.Should().Be(1);
    }

    [Test]
    public void SeasonFor_ShouldAssignDecemberToNextWinterYear()
    {
        var (year, season) = MusicHitParadeCalendar.SeasonFor(new DateTime(2025, 12, 15));

        year.Should().Be(2026);
        season.Should().Be(MusicHitParadeSeasons.Winter);
    }

    [Test]
    public void GetCustomWindow_ShouldIncludeEndDateWithExclusiveNextDay()
    {
        var window = MusicHitParadeCalendar.GetCustomWindow(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 14),
            TimeZoneInfo.Utc);

        window.From.UtcDateTime.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        window.To.UtcDateTime.Should().Be(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void GetCustomWindow_ShouldSwapInvertedDates()
    {
        var window = MusicHitParadeCalendar.GetCustomWindow(
            new DateOnly(2026, 9, 14),
            new DateOnly(2026, 9, 1),
            TimeZoneInfo.Utc);

        window.From.UtcDateTime.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        window.To.UtcDateTime.Should().Be(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void NormalizePeriod_ShouldKeepCustom()
    {
        MusicHitParadeCalendar.NormalizePeriod("custom").Should().Be(MusicHitParadePeriods.Custom);
    }

    [Test]
    public void NormalizeScope_ShouldDefaultToPersonal()
    {
        MusicHitParadeCalendar.NormalizeScope("nope").Should().Be(MusicHitParadeScopes.Personal);
        MusicHitParadeCalendar.NormalizeScope("SERVER").Should().Be(MusicHitParadeScopes.Server);
    }

    [Test]
    public void HasNextPeriod_ShouldBeFalse_WhenNextMonthHasNotStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Month, 2026, 9, null, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
    }

    [Test]
    public void HasNextPeriod_ShouldBeTrue_WhenNextMonthHasStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Month, 2026, 8, null, now, TimeZoneInfo.Utc)
            .Should().BeTrue();
    }

    [Test]
    public void HasNextPeriod_ShouldBeFalse_WhenNextYearHasNotStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Year, 2026, 1, null, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
    }

    [Test]
    public void HasNextPeriod_ShouldBeFalse_WhenNextSeasonHasNotStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Season, 2026, 1, MusicHitParadeSeasons.Autumn, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
    }

    [Test]
    public void HasNextPeriod_ShouldBeTrue_WhenNextSeasonHasStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Season, 2026, 1, MusicHitParadeSeasons.Summer, now, TimeZoneInfo.Utc)
            .Should().BeTrue();
    }

    [Test]
    public void IsFuturePeriod_ShouldBeTrue_WhenMonthHasNotStarted()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.IsFuturePeriod(
            MusicHitParadePeriods.Month, 2026, 10, null, now, TimeZoneInfo.Utc)
            .Should().BeTrue();
    }

    [Test]
    public void IsFuturePeriod_ShouldBeFalse_WhenCurrentMonthIsInProgress()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.IsFuturePeriod(
            MusicHitParadePeriods.Month, 2026, 9, null, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
    }

    [Test]
    public void HasNextPeriod_ShouldBeFalse_WhenPeriodHasNoStep()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.All, 2026, 1, null, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
        MusicHitParadeCalendar.HasNextPeriod(
            MusicHitParadePeriods.Custom, 2026, 1, null, now, TimeZoneInfo.Utc)
            .Should().BeFalse();
    }
}
