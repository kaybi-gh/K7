namespace K7.Server.Domain.Entities.Metadatas.PersonRoles;
public class Actor() : BasePersonRole(PersonRoleType.Actor)
{
    public required string CharacterName { get; set; }
}
