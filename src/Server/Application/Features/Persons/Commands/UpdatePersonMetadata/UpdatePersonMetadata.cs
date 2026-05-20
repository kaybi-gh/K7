using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Persons.Commands.UpdatePersonMetadata;

[Authorize(Roles = Roles.Administrator)]
public record UpdatePersonMetadataCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IList<string> LockedFields { get; init; }

    public string? Name { get; init; }
    public PersonGender? Gender { get; init; }
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
}

public class UpdatePersonMetadataCommandHandler : IRequestHandler<UpdatePersonMetadataCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdatePersonMetadataCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdatePersonMetadataCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, person);

        person.LockedFields = request.LockedFields;

        if (request.Name is not null)
            person.Name = request.Name;
        if (request.Gender is not null)
            person.Gender = request.Gender.Value;
        if (request.Biography is not null)
            person.Biography = request.Biography;
        if (request.Birthday is not null)
            person.Birthday = request.Birthday;
        if (request.Deathday is not null)
            person.Deathday = request.Deathday;
        if (request.BirthPlace is not null)
            person.BirthPlace = request.BirthPlace;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
