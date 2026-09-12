using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationScheduleEvaluatorTests
{
    [Test]
    public void IsWithinWindow_ShouldReturnTrue_WhenNoWindows()
    {
        NotificationScheduleEvaluator.IsWithinWindow([], DateTimeOffset.Now).Should().BeTrue();
    }

    [Test]
    public void IsWithinWindow_ShouldHonorAllDayWindow()
    {
        var windows = new List<NotificationScheduleWindow>
        {
            new()
            {
                Days = [],
                Start = TimeOnly.MinValue,
                End = new TimeOnly(23, 59)
            }
        };

        NotificationScheduleEvaluator.IsWithinWindow(windows, DateTimeOffset.Now).Should().BeTrue();
    }
}
