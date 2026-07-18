using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;

[Authorize(Roles = Roles.Administrator)]
public record UpdateLibraryCommand : IRequest
{
    public Guid Id { get; init; }

    public string? Title { get; init; }
    public string? MetadataProviderName { get; init; }
    public string? MetadataLanguage { get; init; }
    public string? MetadataFallbackLanguage { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public bool? IntroDetectionEnabled { get; init; }
    public bool? ThemeSongGenerationEnabled { get; init; }
    public bool? SeekbarThumbnailGenerationEnabled { get; init; }
    public bool? ChapterExtractionEnabled { get; init; }
    public bool? MusicAudioAnalysisEnabled { get; init; }
    public bool? TranscodingEnabled { get; init; }
    public bool? TransmuxingEnabled { get; init; }
    public bool? RealtimeMonitorEnabled { get; init; }
    public int? AutoScanIntervalHours { get; init; }
}

public class UpdateLibraryCommandHandler(IApplicationDbContext context, ILibraryFolderWatcher libraryFolderWatcher) : IRequestHandler<UpdateLibraryCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly ILibraryFolderWatcher _libraryFolderWatcher = libraryFolderWatcher;

    public async Task Handle(UpdateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        entity.Title = request.Title ?? entity.Title;
        entity.MetadataProviderName = request.MetadataProviderName ?? entity.MetadataProviderName;
        entity.MetadataLanguage = request.MetadataLanguage ?? entity.MetadataLanguage;
        entity.MetadataFallbackLanguage = request.MetadataFallbackLanguage ?? entity.MetadataFallbackLanguage;
        entity.MetadataRefreshIntervalDays = request.MetadataRefreshIntervalDays;
        if (request.LibraryGroupId.HasValue)
            entity.LibraryGroupId = request.LibraryGroupId.Value;
        if (request.IntroDetectionEnabled.HasValue)
            entity.IntroDetectionEnabled = request.IntroDetectionEnabled.Value;
        if (request.ThemeSongGenerationEnabled.HasValue)
            entity.ThemeSongGenerationEnabled = request.ThemeSongGenerationEnabled.Value;
        if (!entity.IntroDetectionEnabled)
            entity.ThemeSongGenerationEnabled = false;
        if (request.SeekbarThumbnailGenerationEnabled.HasValue)
            entity.SeekbarThumbnailGenerationEnabled = request.SeekbarThumbnailGenerationEnabled.Value;
        if (request.ChapterExtractionEnabled.HasValue)
            entity.ChapterExtractionEnabled = request.ChapterExtractionEnabled.Value;
        if (request.MusicAudioAnalysisEnabled.HasValue)
            entity.MusicAudioAnalysisEnabled = request.MusicAudioAnalysisEnabled.Value;
        if (request.TranscodingEnabled.HasValue)
            entity.TranscodingEnabled = request.TranscodingEnabled.Value;
        if (request.TransmuxingEnabled.HasValue)
            entity.TransmuxingEnabled = request.TransmuxingEnabled.Value;
        if (request.RealtimeMonitorEnabled.HasValue)
            entity.RealtimeMonitorEnabled = request.RealtimeMonitorEnabled.Value;
        if (request.AutoScanIntervalHours.HasValue)
            entity.AutoScanIntervalHours = request.AutoScanIntervalHours.Value;
        await _context.SaveChangesAsync(cancellationToken);
        await _libraryFolderWatcher.RefreshWatchersAsync(cancellationToken);
    }
}
