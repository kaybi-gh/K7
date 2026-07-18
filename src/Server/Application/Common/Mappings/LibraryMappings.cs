using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class LibraryMappings
{
    extension(Library domain)
    {
        public LibraryDto ToLibraryDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            MediaType = domain.MediaType,
            RootPath = domain.RootPath,
            MetadataProviderName = domain.MetadataProviderName,
            MetadataLanguage = domain.MetadataLanguage,
            MetadataFallbackLanguage = domain.MetadataFallbackLanguage,
            MetadataRefreshIntervalDays = domain.MetadataRefreshIntervalDays,
            LibraryGroupId = domain.LibraryGroupId,
            PeerServerId = domain.PeerServerId,
            PeerServerName = domain.PeerServer?.Name,
            PeerServerBaseUrl = domain.PeerServer?.BaseUrl,
            PeerReachable = domain.PeerServer is null
                ? null
                : domain.PeerServer.Status == PeerStatus.Active && domain.PeerServer.LastTestSucceeded != false,
            IntroDetectionEnabled = domain.IntroDetectionEnabled,
            ThemeSongGenerationEnabled = domain.ThemeSongGenerationEnabled,
            SeekbarThumbnailGenerationEnabled = domain.SeekbarThumbnailGenerationEnabled,
            ChapterExtractionEnabled = domain.ChapterExtractionEnabled,
            MusicAudioAnalysisEnabled = domain.MusicAudioAnalysisEnabled,
            TranscodingEnabled = domain.TranscodingEnabled,
            TransmuxingEnabled = domain.TransmuxingEnabled,
            RealtimeMonitorEnabled = domain.RealtimeMonitorEnabled,
            AutoScanIntervalHours = domain.AutoScanIntervalHours
        };
    }
}
