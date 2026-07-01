using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsOfflinePage : ComponentBase
{
    private OfflineStorageInfo? _storageInfo;
    private long _maxDownloadBytes;
    private long _maxCacheBytes;
    private bool _allowWifi;
    private bool _allowMobileData;
    private int _lookaheadWifi;
    private int _lookaheadMobile;

    private const long DefaultMaxDownloadBytes = 2L * 1024 * 1024 * 1024;
    private const long DefaultMaxCacheBytes = 500L * 1024 * 1024;
    private const long MinDownloadBytes = 50L * 1024 * 1024;
    private const long MinCacheBytes = 50L * 1024 * 1024;
    private const int DefaultLookaheadWifi = 3;
    private const int DefaultLookaheadMobile = 0;

    protected override async Task OnInitializedAsync()
    {
        _maxDownloadBytes = DeviceStorageService.Get(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES);
        if (_maxDownloadBytes <= 0) _maxDownloadBytes = DefaultMaxDownloadBytes;

        _maxCacheBytes = DeviceStorageService.Get(PreferenceKeys.MAX_CACHE_STORAGE_BYTES);
        if (_maxCacheBytes <= 0) _maxCacheBytes = DefaultMaxCacheBytes;

        _allowWifi = DeviceStorageService.Get(PreferenceKeys.DOWNLOAD_ALLOW_WIFI, true);
        _allowMobileData = DeviceStorageService.Get(PreferenceKeys.DOWNLOAD_ALLOW_MOBILE_DATA);

        _lookaheadWifi = DeviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_WIFI);
        if (_lookaheadWifi <= 0) _lookaheadWifi = DefaultLookaheadWifi;

        _lookaheadMobile = DeviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_MOBILE);

        _storageInfo = await OfflineStore.GetStorageInfoAsync();
        ClampAndPersistStorageLimitsIfNeeded();
    }

    private double GetDownloadStoragePercent()
    {
        if (_storageInfo is null || _maxDownloadBytes <= 0) return 0;
        return Math.Min(100, (double)(_storageInfo.UsedBytes - _storageInfo.CacheBytes) / _maxDownloadBytes * 100);
    }

    private double GetCacheStoragePercent()
    {
        if (_storageInfo is null || _maxCacheBytes <= 0) return 0;
        return Math.Min(100, (double)_storageInfo.CacheBytes / _maxCacheBytes * 100);
    }

    private void OnMaxDownloadChanged(long value) => ApplyStorageLimits(value, _maxCacheBytes);

    private void OnMaxCacheChanged(long value) => ApplyStorageLimits(_maxDownloadBytes, value);

    private void ClampAndPersistStorageLimitsIfNeeded() => ApplyStorageLimits(_maxDownloadBytes, _maxCacheBytes);

    private void ApplyStorageLimits(long downloadBytes, long cacheBytes)
    {
        _maxDownloadBytes = downloadBytes;
        _maxCacheBytes = cacheBytes;
        OfflineStorageLimitHelper.ClampLimits(
            ref _maxDownloadBytes,
            ref _maxCacheBytes,
            _storageInfo?.DeviceTotalBytes,
            MinDownloadBytes,
            MinCacheBytes);

        DeviceStorageService.Set(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES, _maxDownloadBytes);
        DeviceStorageService.Set(PreferenceKeys.MAX_CACHE_STORAGE_BYTES, _maxCacheBytes);
        MusicCacheService.MaxCacheSizeBytes = _maxCacheBytes;
    }

    private void OnAllowWifiChanged(bool value)
    {
        _allowWifi = value;
        DeviceStorageService.Set(PreferenceKeys.DOWNLOAD_ALLOW_WIFI, value);
    }

    private void OnAllowMobileDataChanged(bool value)
    {
        _allowMobileData = value;
        DeviceStorageService.Set(PreferenceKeys.DOWNLOAD_ALLOW_MOBILE_DATA, value);
    }

    private void OnLookaheadWifiChanged(int value)
    {
        _lookaheadWifi = value;
        DeviceStorageService.Set(PreferenceKeys.CACHE_LOOKAHEAD_WIFI, value);
        MusicCacheService.LookaheadCount = value;
    }

    private void OnLookaheadMobileChanged(int value)
    {
        _lookaheadMobile = value;
        DeviceStorageService.Set(PreferenceKeys.CACHE_LOOKAHEAD_MOBILE, value);
    }

    private static string FormatBytes(long bytes) => ByteSizeFormatter.Format(bytes);
}
