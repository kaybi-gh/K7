namespace K7.Clients.MAUI.Constants;

public static class MauiTimeouts
{
    public static readonly TimeSpan ServerReachability = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DownloadRetryDelay = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan WindowsBlazorInitDelay = TimeSpan.FromMilliseconds(500);
}
