namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class Actor() : BasePersonRole(PersonRoleType.Actor)
{
    public required string CharacterName { get; set; }
}
