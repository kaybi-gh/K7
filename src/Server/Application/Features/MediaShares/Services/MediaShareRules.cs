namespace K7.Server.Application.Features.MediaShares.Services;

public static class MediaShareRules
{
    public const int MaxLabelLength = 200;
    public const int MinPasswordLength = 6;

    /// <summary>Wrong passwords in a row before the share stops accepting any for a while.</summary>
    public const int MaxPasswordAttempts = 5;

    public static readonly TimeSpan PasswordLockDuration = TimeSpan.FromMinutes(15);
    public const int MaxPasswordLength = 128;
    public const int MaxAccessCountLimit = 100_000;

    /// <summary>
    /// Share of the runtime after which a single item counts as consumed.
    /// </summary>
    public const double ConsumedProgressRatio = 0.9;

    /// <summary>
    /// Visitor sessions kept in the detail of a share.
    /// </summary>
    public const int MaxListedSessions = 50;
}
