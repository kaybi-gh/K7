using K7.Shared.Dtos.Notifications;

namespace K7.Server.Application.Features.Notifications.Services;

public static class NotificationWebhookPresets
{
    public static IReadOnlyList<NotificationWebhookPresetDto> All { get; } =
    [
        new()
        {
            Id = "discord",
            DisplayNameKey = "PresetDiscord",
            UrlHint = "https://discord.com/api/webhooks/...",
            Method = "POST",
            RawJsonTemplate =
                """
                {
                  "username": "{{Server.Name}}",
                  "embeds": [
                    {
                      "title": "{{Media.Title}}",
                      "description": "{{EventType}}",
                      "url": "{{Media.Url}}",
                      "thumbnail": { "url": "{{PictureUrl}}" },
                      "footer": { "text": "{{Server.Name}} {{Server.Version}}" }
                    }
                  ]
                }
                """
        },
        new()
        {
            Id = "telegram",
            DisplayNameKey = "PresetTelegram",
            UrlHint = "https://api.telegram.org/bot<token>/sendMessage",
            Method = "POST",
            RawJsonTemplate =
                """
                {
                  "chat_id": "<chat-id>",
                  "text": "{{Media.Title}}\n{{EventType}}",
                  "disable_web_page_preview": false
                }
                """
        },
        new()
        {
            Id = "slack",
            DisplayNameKey = "PresetSlack",
            UrlHint = "https://hooks.slack.com/services/...",
            Method = "POST",
            RawJsonTemplate =
                """
                {
                  "text": "{{Server.Name}}: {{Media.Title}} ({{EventType}})"
                }
                """
        },
        new()
        {
            Id = "ntfy",
            DisplayNameKey = "PresetNtfy",
            UrlHint = "https://ntfy.sh/your-topic",
            Method = "POST",
            Headers = new Dictionary<string, string> { ["Title"] = "{{EventType}}" },
            RawJsonTemplate =
                """
                {
                  "topic": "k7",
                  "title": "{{EventType}}",
                  "message": "{{Media.Title}}",
                  "click": "{{Media.Url}}"
                }
                """
        },
        new()
        {
            Id = "gotify",
            DisplayNameKey = "PresetGotify",
            UrlHint = "https://gotify.example.com/message?token=...",
            Method = "POST",
            RawJsonTemplate =
                """
                {
                  "title": "{{EventType}}",
                  "message": "{{Media.Title}}",
                  "priority": 5
                }
                """
        },
        new()
        {
            Id = "signal",
            DisplayNameKey = "PresetSignal",
            UrlHint = "http://signal-cli:8080/v2/send",
            Method = "POST",
            RawJsonTemplate =
                """
                {
                  "message": "{{EventType}}: {{Media.Title}}",
                  "number": "+000",
                  "recipients": ["+000"]
                }
                """
        }
    ];
}
