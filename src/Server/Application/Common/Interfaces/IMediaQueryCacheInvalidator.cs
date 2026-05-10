namespace K7.Server.Application.Common.Interfaces;

public interface IMediaQueryCacheInvalidator
{
    long Version { get; }

    void InvalidateAll();
}
