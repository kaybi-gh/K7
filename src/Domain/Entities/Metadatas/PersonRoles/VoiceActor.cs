namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class VoiceActor() : BasePersonRole(PersonRoleType.VoiceActor)
{
    public required string CharacterName { get; set; }
}
