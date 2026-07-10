namespace K7.Shared;

public sealed class PreferenceKey<T>(string name)
{
    public string Name { get; } = name;
}

public static class PreferenceKeys
{
    public static readonly PreferenceKey<string> K7_SERVER_URL = new("BackendUrl");
    public static readonly PreferenceKey<string> DEVICE_ID = new("DeviceId");
    public static readonly PreferenceKey<string> DEVICE_ATTACHED_USER_ID = new("DeviceAttachedUserId");
    public static readonly PreferenceKey<double> PLAYER_VOLUME = new("PlayerVolume");
    public static readonly PreferenceKey<double> PLAYER_PLAYBACK_RATE = new("PlayerPlaybackRate");
    public static readonly PreferenceKey<bool> PLAYER_IS_MUTED = new("PlayerIsMuted");
    public static readonly PreferenceKey<bool> PLAYER_ADAPTIVE_CROSSFADE = new("PlayerAdaptiveCrossfade");
    public static readonly PreferenceKey<double> PLAYER_CROSSFADE_DURATION = new("PlayerCrossfadeDuration");
    public static readonly PreferenceKey<string> ACCESS_TOKEN = new("AccessToken");
    public static readonly PreferenceKey<string> REFRESH_TOKEN = new("RefreshToken");
    public static readonly PreferenceKey<string> SERVER_INFO = new("ServerInfo");
    public static readonly PreferenceKey<string> LOCAL_USERS = new("LocalUsers");
    public static readonly PreferenceKey<bool> SINGLE_USER_MODE = new("SingleUserMode");
    public static readonly PreferenceKey<string> LAST_ACTIVE_USER_ID = new("LastActiveUserId");
    public static readonly PreferenceKey<string> ACTIVE_SHARED_PROFILE_ID = new("ActiveSharedProfileId");
    public static readonly PreferenceKey<string> LAST_ACTIVE_SHARED_PROFILE_ID = new("LastActiveSharedProfileId");
    public static readonly PreferenceKey<string> LAST_PROFILE_SELECT_KIND = new("LastProfileSelectKind");
    public static readonly PreferenceKey<string> LAST_PROFILE_SELECT_ID = new("LastProfileSelectId");
    public static readonly PreferenceKey<long> LAST_PROFILE_SELECT_AT = new("LastProfileSelectAt");
    public static readonly PreferenceKey<string> SHARED_PROFILES_CACHE = new("SharedProfilesCache");
    public static readonly PreferenceKey<string> PINNED_SHARED_PROFILE_IDS = new("PinnedSharedProfileIds");
    public static readonly PreferenceKey<string> NEXT_EPISODE_BEHAVIOR = new("NextEpisodeBehavior");
    public static readonly PreferenceKey<long> MAX_DOWNLOAD_STORAGE_BYTES = new("MaxDownloadStorageBytes");
    public static readonly PreferenceKey<long> MAX_CACHE_STORAGE_BYTES = new("MaxCacheStorageBytes");
    public static readonly PreferenceKey<bool> DOWNLOAD_ALLOW_MOBILE_DATA = new("DownloadAllowMobileData");
    public static readonly PreferenceKey<bool> DOWNLOAD_ALLOW_WIFI = new("DownloadAllowWifi");
    public static readonly PreferenceKey<int> CACHE_LOOKAHEAD_WIFI = new("CacheLookaheadWifi");
    public static readonly PreferenceKey<int> CACHE_LOOKAHEAD_MOBILE = new("CacheLookaheadMobile");

    // Loudness normalization
    public static readonly PreferenceKey<bool> LOUDNESS_ENABLED = new("LoudnessEnabled");
    public static readonly PreferenceKey<double> LOUDNESS_TARGET_LUFS = new("LoudnessTargetLufs");
    public static readonly PreferenceKey<double> LOUDNESS_PREAMP_DB = new("LoudnessPreampDb");
    public static readonly PreferenceKey<bool> LOUDNESS_LIMITER_ENABLED = new("LoudnessLimiterEnabled");

    // Equalizer
    public static readonly PreferenceKey<bool> EQ_ENABLED = new("EqEnabled");
    public static readonly PreferenceKey<string> EQ_BANDS_JSON = new("EqBandsJson");
    public static readonly PreferenceKey<string> EQ_PRESET_NAME = new("EqPresetName");

    // Streaming quality
    public static readonly PreferenceKey<int> STREAMING_QUALITY_WIFI = new("StreamingQualityWifi");
    public static readonly PreferenceKey<int> STREAMING_QUALITY_MOBILE = new("StreamingQualityMobile");
    public static readonly PreferenceKey<bool> DOWNMIX_TO_STEREO = new("DownmixToStereo");

    // Player UX
    public static readonly PreferenceKey<bool> SHOW_FULLSCREEN_ON_PLAY = new("ShowFullscreenOnPlay");
    public static readonly PreferenceKey<bool> KEEP_SCREEN_ON = new("KeepScreenOn");
    public static readonly PreferenceKey<int> SKIP_BACK_SECONDS = new("SkipBackSeconds");
    public static readonly PreferenceKey<int> SKIP_FORWARD_SECONDS = new("SkipForwardSeconds");

    // Sleep timer
    public static readonly PreferenceKey<string> SLEEP_TIMER_MODE = new("SleepTimerMode");

    // Autoplay / Radio
    public static readonly PreferenceKey<bool> AUTOPLAY_ENABLED = new("AutoplayEnabled");
    public static readonly PreferenceKey<int> RADIO_DEVIATION_DEGREE = new("RadioDeviationDegree");

    // SyncPlay
    public static readonly PreferenceKey<bool> SYNCPLAY_ENABLED = new("SyncPlayEnabled");

    // Page sidebar (admin / settings)
    public static readonly PreferenceKey<bool> PAGE_SIDEBAR_COLLAPSED = new("PageSidebarCollapsed");
}
