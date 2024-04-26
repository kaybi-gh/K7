namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class VoiceActor() : BasePersonRole(PersonJob.VoiceActor)
{
    public required string CharacterName { get; set; }
}
