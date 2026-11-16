namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Visitor access decisions are cached for a few seconds. Anything that changes who may
/// access a share (revoke, delete, edit, new link, server switch) bumps the version so
/// cached decisions stop being used at once.
/// </summary>
public interface IShareAccessInvalidator
{
    long Version { get; }

    void Invalidate();
}

public sealed class ShareAccessInvalidator : IShareAccessInvalidator
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Invalidate() => Interlocked.Increment(ref _version);
}
