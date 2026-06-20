using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.LibraryGroups.Commands.UpdateLibraryGroup;

[Authorize(Roles = Roles.Administrator)]
public record UpdateLibraryGroupCommand : IRequest
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? CardColor { get; init; }
}

public class UpdateLibraryGroupCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateLibraryGroupCommand>
{
    public async Task Handle(UpdateLibraryGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.LibraryGroups
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        if (request.Title is not null) entity.Title = request.Title;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.Icon is not null) entity.Icon = request.Icon;
        if (request.CardColor is not null) entity.CardColor = string.IsNullOrEmpty(request.CardColor) ? null : request.CardColor;

        await context.SaveChangesAsync(cancellationToken);
    }
}
