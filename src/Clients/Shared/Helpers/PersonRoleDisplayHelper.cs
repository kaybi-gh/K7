using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Clients.Shared.Helpers;

public static class PersonRoleDisplayHelper
{
    public sealed record GroupedDisplay(Guid Key, LitePersonRoleDto PrimaryRole, string? MergedSubtitle);

    public static IReadOnlyList<GroupedDisplay> GroupForCarousel(IEnumerable<LitePersonRoleDto>? roles)
    {
        if (roles is null)
            return [];

        var sortedRoles = roles
            .Where(r => r.Person is not null)
            .OrderBy(r => r.Order is null)
            .ThenBy(r => r.Order)
            .ToList();

        var firstIndexByPerson = new Dictionary<Guid, int>();
        var rolesByPerson = new Dictionary<Guid, List<LitePersonRoleDto>>();

        for (var i = 0; i < sortedRoles.Count; i++)
        {
            var role = sortedRoles[i];
            var personId = role.Person!.Id;

            firstIndexByPerson.TryAdd(personId, i);

            if (!rolesByPerson.TryGetValue(personId, out var personRoles))
            {
                personRoles = [];
                rolesByPerson[personId] = personRoles;
            }

            personRoles.Add(role);
        }

        return rolesByPerson
            .Select(kvp => new GroupedDisplay(
                Key: kvp.Key,
                PrimaryRole: kvp.Value[0],
                MergedSubtitle: BuildMergedSubtitle(kvp.Value)))
            .OrderBy(x => firstIndexByPerson[x.Key])
            .ToList();
    }

    private static string? BuildMergedSubtitle(IReadOnlyList<LitePersonRoleDto> rolesInDisplayOrder)
    {
        var parts = new List<string>();
        foreach (var role in rolesInDisplayOrder)
        {
            var part = role switch
            {
                LiteActorDto actor when !string.IsNullOrWhiteSpace(actor.CharacterName) => actor.CharacterName,
                LiteCrewMemberDto crew => FormatCrewSubtitle(crew),
                LiteMusicArtistRoleDto artist when !string.IsNullOrWhiteSpace(artist.Role) => artist.Role,
                _ => null
            };

            if (part is not null && !parts.Exists(p => string.Equals(p, part, StringComparison.OrdinalIgnoreCase)))
                parts.Add(part);
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    private static string? FormatCrewSubtitle(LiteCrewMemberDto crew)
    {
        if (!string.IsNullOrWhiteSpace(crew.Job) && !string.IsNullOrWhiteSpace(crew.Department))
            return $"{crew.Department} / {crew.Job}";

        return crew.Job ?? crew.Department;
    }
}
