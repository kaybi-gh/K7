namespace MediaServer.Domain.Entities.Metadatas.PersonRoles;
public class VoiceActor() : BasePersonRole(PersonRoleType.VoiceActor)
{
    public required string CharacterName { get; set; }
}
