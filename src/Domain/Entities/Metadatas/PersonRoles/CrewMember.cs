namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class CrewMember() : BasePersonRole(PersonRoleType.CrewMember)
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}
