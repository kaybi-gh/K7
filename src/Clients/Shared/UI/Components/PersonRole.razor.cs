using K7.Shared.Dtos.Entities.PersonRoles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PersonRole
{
    [Inject] private IK7ServerService k7ServerService { get; set; } = default!;

    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public required LitePersonRoleDto LitePersonRoleDto { get; set; }

    private string? GetImageUrl()
    {
        var picture = LitePersonRoleDto.PortraitPicture ?? LitePersonRoleDto.Person?.PortraitPicture;
        return k7ServerService.GetAbsoluteUri(
            picture?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private string? GetSubtitle() => LitePersonRoleDto switch
    {
        LiteCrewMemberDto crew => $"{crew.Department} / {crew.Job}",
        LiteActorDto actor => actor.CharacterName,
        LiteMusicArtistRoleDto artist when !string.IsNullOrEmpty(artist.Role) => artist.Role,
        _ => null,
    };
}
