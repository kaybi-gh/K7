namespace K7.Shared.Dtos.Notifications;

public sealed record NotificationEventDescriptorDto
{
    public required string EventTypeName { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required string DefaultTitleTemplate { get; init; }
    public required string DefaultBodyTemplate { get; init; }
    public required IReadOnlyList<NotificationParameterInfoDto> Parameters { get; init; }
}

public sealed record NotificationParameterInfoDto
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string ValueType { get; init; }
}
