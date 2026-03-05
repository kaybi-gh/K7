using FFMpegCore.Helpers;
using FFMpegCore;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Infrastructure.MediaProcessing;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaProcessingServices(this IServiceCollection services)
    {
        services.AddSingleton<IMediaAnalysisService, MediaAnalysisService>();
        services.AddSingleton<IMediaTranscoder, MediaTranscoder>();
        services.AddSingleton<IMediaFormatSampleGenerator, MediaFormatSampleGenerator>();
        services.AddSingleton<ITranscodeJobManager, TranscodeJobManager>();
        services.AddHostedService<TranscodeJobCleanupService>();
        services.AddScoped<TMDbMetadataProvider>();
        services.AddScoped<IMetadataProvider<ExternalMovieMetadata>>(sp => sp.GetRequiredService<TMDbMetadataProvider>());
        services.AddScoped<ISearchableMetadataProvider>(sp => sp.GetRequiredService<TMDbMetadataProvider>()); // TODO - Make it customizable

        services.AddSignalR();
        return services;
    }

    public static void InitializeMediaProcessing(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var pathConfiguration = scope.ServiceProvider.GetRequiredService<IOptions<PathsConfiguration>>().Value;

        GlobalFFOptions.Configure(new FFOptions()
        {
            BinaryFolder = pathConfiguration.FFMpegBinaryFolder ?? ""
        });
        FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
    }
}
