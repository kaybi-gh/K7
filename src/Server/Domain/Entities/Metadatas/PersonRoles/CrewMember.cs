namespace K7.Server.Domain.Entities.Metadatas.PersonRoles;
public class CrewMember() : BasePersonRole(PersonRoleType.CrewMember)
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}
