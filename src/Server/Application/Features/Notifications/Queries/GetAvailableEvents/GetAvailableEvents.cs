using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Notifications.Queries.GetAvailableEvents;

public record NotificationEventDescriptorDto(
    string EventTypeName,
    string DisplayName,
    string Category,
    string DefaultTitleTemplate,
    string DefaultBodyTemplate,
    IReadOnlyList<NotificationParameterInfoDto> Parameters);

public record NotificationParameterInfoDto(string Name, string DisplayName, string ValueType);

[Authorize(Roles = Roles.Administrator)]
public record GetAvailableEventsQuery : IRequest<IEnumerable<NotificationEventDescriptorDto>>;

public class GetAvailableEventsQueryHandler : IRequestHandler<GetAvailableEventsQuery, IEnumerable<NotificationEventDescriptorDto>>
{
    private readonly IEnumerable<INotificationEventDescriptor> _descriptors;

    public GetAvailableEventsQueryHandler(IEnumerable<INotificationEventDescriptor> descriptors)
    {
        _descriptors = descriptors;
    }

    public Task<IEnumerable<NotificationEventDescriptorDto>> Handle(GetAvailableEventsQuery request, CancellationToken cancellationToken)
    {
        var globalParams = GlobalNotificationParameters.All
            .Select(p => new NotificationParameterInfoDto(p.Name, p.DisplayName, p.ValueType))
            .ToList();

        var result = _descriptors.Select(d => new NotificationEventDescriptorDto(
            d.EventTypeName,
            d.DisplayName,
            d.Category.ToString(),
            d.DefaultTitleTemplate,
            d.DefaultBodyTemplate,
            d.Parameters.Select(p => new NotificationParameterInfoDto(p.Name, p.DisplayName, p.ValueType))
                .Concat(globalParams)
                .ToList()));

        return Task.FromResult(result);
    }
}
