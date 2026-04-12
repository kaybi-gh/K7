using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Commands.BulkLinkArtists;

[Authorize(Roles = Roles.Administrator)]
public record BulkLinkArtistsCommand : IRequest<int>
{
    public required IReadOnlyList<BulkLinkArtistsRequest.ArtistLinkItem> Items { get; init; }
}

public class BulkLinkArtistsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BulkLinkArtistsCommand, int>
{
    public async Task<int> Handle(BulkLinkArtistsCommand request, CancellationToken cancellationToken)
    {
        var artistNames = request.Items
            .Select(i => i.ArtistName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Find or create persons
        var existingPersons = await context.Persons
            .Where(p => artistNames.Contains(p.Name))
            .ToListAsync(cancellationToken);

        var personCache = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in existingPersons)
        {
            personCache.TryAdd(person.Name, person);
        }

        var newPersons = new List<Person>();
        foreach (var name in artistNames)
        {
            if (!personCache.ContainsKey(name))
            {
                var person = new Person { Name = name };
                context.Persons.Add(person);
                newPersons.Add(person);
                personCache[name] = person;
            }
        }

        if (newPersons.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Find existing roles to avoid duplicates
        var mediaIds = request.Items.Select(i => i.MediaId).Distinct().ToList();
        var personIds = personCache.Values.Select(p => p.Id).ToList();

        var existingRoles = await context.PersonRoles
            .OfType<MusicArtist>()
            .Where(r => mediaIds.Contains(r.MediaId) && personIds.Contains(r.PersonId))
            .Select(r => new { r.PersonId, r.MediaId })
            .ToListAsync(cancellationToken);

        var existingRoleKeys = existingRoles
            .Select(r => (r.PersonId, r.MediaId))
            .ToHashSet();

        // Create missing roles
        var created = 0;
        foreach (var item in request.Items)
        {
            if (!personCache.TryGetValue(item.ArtistName, out var person)) continue;
            if (existingRoleKeys.Contains((person.Id, item.MediaId))) continue;

            existingRoleKeys.Add((person.Id, item.MediaId));
            context.PersonRoles.Add(new MusicArtist
            {
                PersonId = person.Id,
                MediaId = item.MediaId,
                IsGuest = false
            });
            created++;
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
