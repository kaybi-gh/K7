using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;
public partial class PersonRole
{
    [Parameter] public bool Skeleton { get; set; } = false;
    [Parameter] public required LitePersonRoleDto LitePersonRoleDto { get; set; }
}
