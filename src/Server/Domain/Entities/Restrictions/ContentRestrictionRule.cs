using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Restrictions;

public class ContentRestrictionRule
{
    public RestrictionField Field { get; set; }
    public RestrictionOperator Operator { get; set; }
    public string? Value { get; set; }
}
