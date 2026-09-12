using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Notifications.Services;

public record NotificationParameterInfo(
    string Name,
    string DisplayNameKey,
    string ValueType,
    NotificationParameterGroup Group,
    string SampleValue,
    RuleFieldValueType FilterValueType = RuleFieldValueType.Text,
    IReadOnlyList<RuleFieldOptionDto>? FilterOptions = null);

public interface INotificationEventDescriptor
{
    string EventTypeName { get; }
    string DisplayNameKey { get; }
    NotificationEventCategory Category { get; }
    string DefaultTitleTemplate { get; }
    string DefaultBodyTemplate { get; }
    IReadOnlyList<NotificationParameterInfo> Parameters { get; }
}
