namespace K7.Shared.Scrobbling;

/// <summary>
/// Public setup pages where admins or users obtain API keys / tokens for scrobbling.
/// Self-hosted webhook apps use <c>yourdomain.tld</c> placeholders in preset URL hints instead.
/// </summary>
public static class ScrobblerSetupUrls
{
    public const string ListenBrainzTokenSettings = "https://listenbrainz.org/settings/";
    public const string LastFmApiAccountCreate = "https://www.last.fm/api/account/create";
    public const string TraktOauthApplications = "https://trakt.tv/oauth/applications";
    public const string BetaSeriesApi = "https://www.betaseries.com/api/";
}
