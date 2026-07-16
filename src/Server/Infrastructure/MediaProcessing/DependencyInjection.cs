using FFMpegCore.Helpers;
using FFMpegCore;
using K7.Server.Domain.Interfaces;
using K7.Server.Application.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;
using K7.Server.Application.Common.Interfaces;
using TMDbLib.Client;

namespace K7.Server.Infrastructure.MediaProcessing;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaProcessingServices(this IServiceCollection services)
    {
        services.AddSingleton<IMediaAnalysisService, MediaAnalysisService>();
        services.AddSingleton<IFfmpegCapabilitiesService, FfmpegCapabilitiesService>();
        services.AddSingleton<IMediaTranscoder, MediaTranscoder>();
        services.AddSingleton<IImageProcessor, ImageProcessor>();
        services.AddSingleton<ITranscodeJobManager, TranscodeJobManager>();
        services.AddSingleton<IAudioTagReader, TagLibAudioTagReader>();
        services.AddSingleton<IWaveformGenerator, FfmpegWaveformGenerator>();
        services.AddSingleton<IFadeAnalyzer, FfmpegFadeAnalyzer>();
        services.AddSingleton<ILoudnessAnalyzer, FfmpegLoudnessAnalyzer>();
        services.AddSingleton<IChromaprintService, ChromaprintService>();
        services.AddSingleton<ISegmentDetectionService, SegmentDetectionService>();
        services.AddSingleton<IEpisodeStillGenerator, EpisodeStillGenerator>();
        services.AddHostedService<TranscodeJobCleanupService>();
        services.AddSingleton<TMDbClient>(sp =>
        {
            var client = new TMDbClient("8e7586ad850237f5d506d8789f4c3936");
            client.SetConfig(client.GetConfigAsync().GetAwaiter().GetResult());
            return client;
        });
        services.AddHttpClient<TvdbAuthenticationService>();
        services.AddScoped<TMDbMetadataProvider>();
        services.AddScoped<IMetadataProvider<ExternalMovieMetadata>>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddKeyedScoped<IMetadataProvider<ExternalMovieMetadata>>("tmdb", (sp, _) => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddKeyedScoped<IMetadataProvider<ExternalMovieMetadata>>("imdb", (sp, _) => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<ISearchableMetadataProvider>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<IMetadataProviderInfo>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<IPersonMetadataProvider>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<IMetadataImageProvider>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<IPersonImageProvider>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<MusicBrainzPersonMetadataProvider>();
        services.AddScoped<IPersonMetadataProvider>(sp => sp.GetRequiredService<MusicBrainzPersonMetadataProvider>());
        services.AddScoped<IPersonImageProvider>(sp => sp.GetRequiredService<MusicBrainzPersonMetadataProvider>());
        services.AddScoped<TMDbSerieMetadataProvider>();
        services.AddKeyedScoped<ISerieMetadataProvider>("tmdb", (sp, _) => sp.GetRequiredService<TMDbSerieMetadataProvider>());
        services.AddKeyedScoped<ISerieMetadataProvider>("imdb", (sp, _) => sp.GetRequiredService<TMDbSerieMetadataProvider>());
        services.AddScoped<ISearchableMetadataProvider>(sp => sp.GetRequiredService<TMDbSerieMetadataProvider>());
        services.AddScoped<IMetadataImageProvider>(sp => sp.GetRequiredService<TMDbSerieMetadataProvider>());
        services.AddHttpClient<TvdbApiClient>();
        services.AddScoped<TvdbSerieMetadataProvider>();
        services.AddKeyedScoped<ISerieMetadataProvider>("tvdb", (sp, _) => sp.GetRequiredService<TvdbSerieMetadataProvider>());
        services.AddScoped<ISearchableMetadataProvider>(sp => sp.GetRequiredService<TvdbSerieMetadataProvider>());
        services.AddScoped<IMetadataProviderInfo>(sp => sp.GetRequiredService<TvdbSerieMetadataProvider>());
        services.AddScoped<IMetadataImageProvider>(sp => sp.GetRequiredService<TvdbSerieMetadataProvider>());
        services.AddScoped<MusicBrainzMetadataProvider>();
        services.AddScoped<IMetadataProvider<ExternalMusicAlbumMetadata>>(sp => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddKeyedScoped<IMetadataProvider<ExternalMusicAlbumMetadata>>("musicbrainz", (sp, _) => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddScoped<IMusicArtistMetadataProvider>(sp => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddScoped<IMetadataProviderInfo>(sp => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddScoped<ISearchableMetadataProvider>(sp => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddScoped<IMetadataImageProvider>(sp => sp.GetRequiredService<MusicBrainzMetadataProvider>());
        services.AddScoped<IMusicArtistMetadataProvider, WikidataMetadataProvider>();
        services.AddScoped<IPersonCreditsProvider, TMDbPersonCreditsProvider>();

        services.AddScoped<FederationMetadataProvider>();
        services.AddKeyedScoped<IMetadataProvider<ExternalMovieMetadata>>("federation", (sp, _) => sp.GetRequiredService<FederationMetadataProvider>());
        services.AddKeyedScoped<ISerieMetadataProvider>("federation", (sp, _) => sp.GetRequiredService<FederationMetadataProvider>());
        services.AddKeyedScoped<IMetadataProvider<ExternalMusicAlbumMetadata>>("federation", (sp, _) => sp.GetRequiredService<FederationMetadataProvider>());
        services.AddScoped<IMetadataProviderInfo>(sp => sp.GetRequiredService<FederationMetadataProvider>());

        services.AddSignalR();
        return services;
    }

    public static void InitializeMediaProcessing(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("SmokeTest:SkipFfmpegVerification"))
            return;

        using var scope = app.Services.CreateScope();
        var pathConfiguration = scope.ServiceProvider.GetRequiredService<IOptions<PathsConfiguration>>().Value;

        GlobalFFOptions.Configure(new FFOptions()
        {
            BinaryFolder = pathConfiguration.FFMpegBinaryFolder ?? ""
        });
        FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
    }
}
