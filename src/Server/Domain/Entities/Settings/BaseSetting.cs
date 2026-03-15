using K7.Server.Domain.Common;

namespace K7.Server.Domain.Entities.Settings;

public abstract class BaseSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
