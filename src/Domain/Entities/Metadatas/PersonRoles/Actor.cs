namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class Actor() : BasePersonRole(PersonJob.Actor)
{
    public required string CharacterName { get; set; }
}
