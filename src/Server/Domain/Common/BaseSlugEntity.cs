namespace K7.Server.Domain.Common;
public abstract class BaseSlugEntity : BaseAuditableEntity
{
    public string Slug { get; set; } = null!;
    public abstract string GetSlugSource();
}
