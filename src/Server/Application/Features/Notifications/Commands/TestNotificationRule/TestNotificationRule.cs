using System.Net.Http;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Commands.TestNotificationRule;

public record TestNotificationRuleResult(bool Success, string? Error = null);

[Authorize(Roles = Roles.Administrator)]
public record TestNotificationRuleCommand(Guid Id) : IRequest<TestNotificationRuleResult>;

public class TestNotificationRuleCommandHandler : IRequestHandler<TestNotificationRuleCommand, TestNotificationRuleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationPayloadRenderer _payloadRenderer;
    private readonly ILogger<TestNotificationRuleCommandHandler> _logger;

    public TestNotificationRuleCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        NotificationPayloadRenderer payloadRenderer,
        ILogger<TestNotificationRuleCommandHandler> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _payloadRenderer = payloadRenderer;
        _logger = logger;
    }

    public async Task<TestNotificationRuleResult> Handle(TestNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var now = DateTime.UtcNow;
        var testData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EventType"] = entity.EventTypeNames.FirstOrDefault() ?? "TestEvent",
            ["Current.Year"] = now.Year,
            ["Current.Month"] = now.Month,
            ["Current.Day"] = now.Day,
            ["Current.Hour"] = now.Hour,
            ["Current.Minute"] = now.Minute,
            ["Current.Weekday"] = now.DayOfWeek.ToString(),
            ["Current.Datestamp"] = now.ToString("yyyy-MM-dd"),
            ["Current.Timestamp"] = now.ToString("o"),
            ["Media.Title"] = "Interstellar",
            ["Media.OriginalTitle"] = "Interstellar",
            ["Media.MediaType"] = "Movie",
            ["Media.Type"] = "Movie",
            ["Media.ReleaseYear"] = 2014,
            ["Media.Genres.Count"] = 3,
            ["Media.IndexedFiles.Count"] = 1,
            ["PictureUrl"] = "https://k7.example.com/api/metadata-pictures/a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            ["BackdropUrl"] = "https://k7.example.com/api/metadata-pictures/f9e8d7c6-b5a4-3210-fedc-ba9876543210",
            ["Library.Title"] = "Films",
            ["Library.MediaType"] = "Movies",
            ["Library.MetadataProviderName"] = "TMDB",
            ["Library.MetadataLanguage"] = "fr",
            ["Device.Name"] = "Galaxy S24",
            ["Device.OperatingSystemVersion"] = "Android 15",
            ["Device.DisplayWidth"] = 1080,
            ["Device.DisplayHeight"] = 2340,
            ["Device.DeviceUniqueId"] = "abc123-test",
            ["Device.LastSeen"] = DateTime.UtcNow.ToString("o"),
            ["Playlist.Title"] = "Road Trip",
            ["Playlist.Description"] = "Songs for the road",
            ["Playlist.Items.Count"] = 42,
            ["PlaylistItem.MediaId"] = Guid.NewGuid().ToString(),
            ["IndexedFile.FileName"] = "interstellar.mkv",
            ["IndexedFile.ParentDirectory"] = "/media/movies",
            ["IndexedFile.Size"] = 4_200_000_000L,
            ["IndexedFile.LibraryId"] = Guid.NewGuid().ToString(),
            ["Download.IndexedFileId"] = Guid.NewGuid().ToString(),
            ["Download.DeviceId"] = Guid.NewGuid().ToString(),
            ["Download.UserId"] = Guid.NewGuid().ToString(),
            ["SmartPlaylist.Title"] = "Recently Added",
            ["SmartPlaylist.Description"] = "Last 30 days",
            ["SmartPlaylist.OrderDirection"] = "Descending",
            ["Collection.Title"] = "Marvel",
            ["Collection.Description"] = "MCU movies",
            ["Collection.Items.Count"] = 33,
            ["Session.UserId"] = Guid.NewGuid().ToString()
        };

        var title = _payloadRenderer.Render(entity.TitleTemplate, testData);
        var body = _payloadRenderer.Render(entity.BodyTemplate, testData);
        var provider = _serviceProvider.GetRequiredKeyedService<INotificationProvider>(entity.ProviderType);

        var payload = entity.PayloadFormat == NotificationPayloadFormat.RawJson
            ? _payloadRenderer.Render(entity.RawJsonTemplate, testData)
            : JsonSerializer.Serialize(new { title, body });

        try
        {
            var success = await provider.SendAsync(entity.ProviderConfig, payload, cancellationToken);

            _logger.LogInformation("Test notification for rule {RuleId} ({RuleName}): {Result}",
                entity.Id, entity.Name, success ? "success" : "failed");

            return success
                ? new TestNotificationRuleResult(true)
                : new TestNotificationRuleResult(false, "Webhook returned a non-success status code");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Test notification for rule {RuleId} failed", entity.Id);
            return new TestNotificationRuleResult(false, ex.InnerException?.Message ?? ex.Message);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Test notification for rule {RuleId} timed out", entity.Id);
            return new TestNotificationRuleResult(false, "Request timed out");
        }
    }
}
