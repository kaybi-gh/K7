namespace K7.Server.Application.Common.Interfaces;

public interface IDatabaseCapabilities
{
    bool SupportsTrigramSearch { get; }
}
