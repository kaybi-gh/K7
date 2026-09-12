using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Notifications.Queries.GetAvailableEvents;

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
        var globalParams = NotificationParams.Globals.Select(ToDto).ToList();

        var result = _descriptors.Select(d => new NotificationEventDescriptorDto
        {
            EventTypeName = d.EventTypeName,
            DisplayName = d.DisplayNameKey,
            DisplayNameKey = d.DisplayNameKey,
            Category = d.Category.ToString(),
            DefaultTitleTemplate = d.DefaultTitleTemplate,
            DefaultBodyTemplate = d.DefaultBodyTemplate,
            Parameters = d.Parameters.Select(ToDto).Concat(globalParams).ToList()
        });

        return Task.FromResult(result);
    }

    private static NotificationParameterInfoDto ToDto(NotificationParameterInfo p) => new()
    {
        Name = p.Name,
        DisplayName = p.DisplayNameKey,
        DisplayNameKey = p.DisplayNameKey,
        ValueType = p.ValueType,
        Group = p.Group.ToString(),
        SampleValue = p.SampleValue,
        FilterValueType = p.FilterValueType.ToString(),
        FilterOptions = p.FilterOptions
    };
}
