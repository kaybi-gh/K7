using System.Reflection;
using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Application.Features.Home.Services;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Application.Features.Notifications.EventHandlers;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Application.Features.Notifications.Services.Descriptors;
using K7.Server.Application.Services;
using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application;

public static class DependencyInjection
{
    public const string MetadataPictureDownloadClient = "MetadataPictureDownload";

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient(string.Empty, client =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        });

        services.AddHttpClient(MetadataPictureDownloadClient, client =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigureAdditionalHttpMessageHandlers((handlers, _) => handlers.Clear());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<LiteMediaProjectionService>();
        services.AddScoped<MediaAccessFilter>();
        services.AddScoped<IMediaLibraryAvailabilityService, MediaLibraryAvailabilityService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddSingleton<BackgroundTaskQueue>();
        services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
        services.AddSingleton<BackgroundTaskTypeRegistry>();
        services.AddSingleton<BackgroundTasksProcessingService>();
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundTasksProcessingService>());
        services.AddHostedService<MetadataRefreshSchedulerService>();
        services.AddHostedService<PeerSyncSchedulerService>();
        services.AddHostedService<LibraryScanSchedulerService>();
        services.AddSingleton<LibraryFolderWatcherService>();
        services.AddSingleton<ILibraryFolderWatcher>(sp => sp.GetRequiredService<LibraryFolderWatcherService>());
        services.AddHostedService(sp => sp.GetRequiredService<LibraryFolderWatcherService>());
        services.AddSingleton<ActiveStreamTracker>();
        services.AddSingleton<IActiveStreamTracker>(sp => sp.GetRequiredService<ActiveStreamTracker>());
        services.AddSingleton<HubPresenceTracker>();
        services.AddSingleton<IHubPresenceTracker>(sp => sp.GetRequiredService<HubPresenceTracker>());
        services.AddSingleton<ServerDiskMetricsProvider>();
        services.AddSingleton<IServerDiskMetricsProvider>(sp => sp.GetRequiredService<ServerDiskMetricsProvider>());
        services.AddSingleton<ServerMetricsCollector>();
        services.AddSingleton<IServerMetricsCollector>(sp => sp.GetRequiredService<ServerMetricsCollector>());
        services.AddSingleton<SyncPlayCoordinator>();
        services.AddSingleton<ISyncPlayCoordinator>(sp => sp.GetRequiredService<SyncPlayCoordinator>());
        services.AddHostedService<SyncPlayStaleGroupCleanupService>();
        services.AddSingleton<BoundedMemoryCache>();
        services.AddSingleton<IBoundedMemoryCache>(sp => sp.GetRequiredService<BoundedMemoryCache>());
        services.AddSingleton<IMediaQueryCacheInvalidator>(sp => sp.GetRequiredService<MediaQueryCacheInvalidator>());
        services.AddSingleton(sp =>
        {
            var limiter = new OutboundRateLimiter();
            limiter.ConfigureHost("musicbrainz.org", TimeSpan.FromMilliseconds(1100));
            limiter.ConfigureHost("api4.thetvdb.com", TimeSpan.FromMilliseconds(500));
            limiter.ConfigureHost("artworks.thetvdb.com", TimeSpan.FromMilliseconds(200));
            limiter.ConfigureHost("image.tmdb.org", TimeSpan.FromMilliseconds(50));
            limiter.ConfigureHost("commons.wikimedia.org", TimeSpan.FromSeconds(2));
            limiter.ConfigureHost("upload.wikimedia.org", TimeSpan.FromSeconds(2));
            return limiter;
        });
        services.AddScoped<IBackgroundTaskExecutionContext, BackgroundTaskExecutionContext>();
        services.AddScoped<IFileIndexer, FileIndexer>();
        services.AddScoped<ILibraryScanProgressReporter, LibraryScanProgressReporter>();
        services.AddScoped<IMediaAccessGuard, MediaAccessGuard>();
        services.AddScoped<IUserMediaStateUpdater, UserMediaStateUpdater>();
        services.AddScoped<IPlaybackPolicySettingsProvider, PlaybackPolicySettingsProvider>();
        services.AddScoped<IContinueWatchingExclusionService, ContinueWatchingExclusionService>();
        services.AddScoped<ISharedProfilePlaybackResolver, SharedProfilePlaybackResolver>();
        services.AddScoped<ISyncPlayPlaybackContextResolver, SyncPlayPlaybackContextResolver>();
        services.AddScoped<IMediaMetadataTagSyncService, MediaMetadataTagSyncService>();
        services.AddScoped<MediaExternalIdResolver>();
        services.AddScoped<MediaPictureReadyNotifier>();
        services.AddScoped<MetadataPictureDeletionService>();
        services.AddScoped<DiagnosticIssueEntityResolver>();
        services.AddScoped<OrphanIndexedFileFixBuilder>();
        services.AddScoped<DiagnosticFixBatchBuilder>();
        services.AddScoped<IHomeLayoutMaintenanceService, HomeLayoutMaintenanceService>();
        services.AddScoped<INextEpisodeEnqueueService, NextEpisodeEnqueueService>();

        // Outbound Notifications
        services.AddSingleton<NotificationConditionEvaluator>();
        services.AddSingleton<NotificationPayloadRenderer>();
        services.AddSingleton<NotificationEventDataSerializer>();
        services.AddScoped<OutboundNotificationDispatcher>();

        services.AddSingleton<INotificationEventDescriptor, MediaAddedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, LibraryCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, LibraryDeletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, LibraryFilesIndexTriggeredEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, MediaPlaybackCompletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaybackStateChangedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaylistCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaylistUpdatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaylistDeletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaylistItemAddedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, PlaylistItemRemovedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, DeviceCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, DeviceUpdatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, DeviceDeletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, CollectionCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, CollectionDeletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, SmartPlaylistCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, SmartPlaylistUpdatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, SmartPlaylistDeletedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, DownloadReadyEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, IndexedFileCreatedEventDescriptor>();
        services.AddSingleton<INotificationEventDescriptor, IndexedFileDeletedEventDescriptor>();

        // Register outbound notification handler for each event type
        services.AddTransient<INotificationHandler<MediaAddedEvent>, OutboundNotificationEventHandler<MediaAddedEvent>>();
        services.AddTransient<INotificationHandler<LibraryCreatedEvent>, OutboundNotificationEventHandler<LibraryCreatedEvent>>();
        services.AddTransient<INotificationHandler<LibraryDeletedEvent>, OutboundNotificationEventHandler<LibraryDeletedEvent>>();
        services.AddTransient<INotificationHandler<LibraryFilesIndexTriggeredEvent>, OutboundNotificationEventHandler<LibraryFilesIndexTriggeredEvent>>();
        services.AddTransient<INotificationHandler<PlaybackStateChangedEvent>, OutboundNotificationEventHandler<PlaybackStateChangedEvent>>();
        services.AddTransient<INotificationHandler<PlaylistCreatedEvent>, OutboundNotificationEventHandler<PlaylistCreatedEvent>>();
        services.AddTransient<INotificationHandler<PlaylistUpdatedEvent>, OutboundNotificationEventHandler<PlaylistUpdatedEvent>>();
        services.AddTransient<INotificationHandler<PlaylistDeletedEvent>, OutboundNotificationEventHandler<PlaylistDeletedEvent>>();
        services.AddTransient<INotificationHandler<PlaylistItemAddedEvent>, OutboundNotificationEventHandler<PlaylistItemAddedEvent>>();
        services.AddTransient<INotificationHandler<PlaylistItemRemovedEvent>, OutboundNotificationEventHandler<PlaylistItemRemovedEvent>>();
        services.AddTransient<INotificationHandler<DeviceCreatedEvent>, OutboundNotificationEventHandler<DeviceCreatedEvent>>();
        services.AddTransient<INotificationHandler<DeviceUpdatedEvent>, OutboundNotificationEventHandler<DeviceUpdatedEvent>>();
        services.AddTransient<INotificationHandler<DeviceDeletedEvent>, OutboundNotificationEventHandler<DeviceDeletedEvent>>();
        services.AddTransient<INotificationHandler<CollectionCreatedEvent>, OutboundNotificationEventHandler<CollectionCreatedEvent>>();
        services.AddTransient<INotificationHandler<CollectionDeletedEvent>, OutboundNotificationEventHandler<CollectionDeletedEvent>>();
        services.AddTransient<INotificationHandler<SmartPlaylistCreatedEvent>, OutboundNotificationEventHandler<SmartPlaylistCreatedEvent>>();
        services.AddTransient<INotificationHandler<SmartPlaylistUpdatedEvent>, OutboundNotificationEventHandler<SmartPlaylistUpdatedEvent>>();
        services.AddTransient<INotificationHandler<SmartPlaylistDeletedEvent>, OutboundNotificationEventHandler<SmartPlaylistDeletedEvent>>();
        services.AddTransient<INotificationHandler<DownloadReadyEvent>, OutboundNotificationEventHandler<DownloadReadyEvent>>();
        services.AddTransient<INotificationHandler<IndexedFileCreatedEvent>, OutboundNotificationEventHandler<IndexedFileCreatedEvent>>();
        services.AddTransient<INotificationHandler<IndexedFileDeletedEvent>, OutboundNotificationEventHandler<IndexedFileDeletedEvent>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<BaseMedia>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<BaseMedia>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<MusicTrack>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<MusicTrack>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<Movie>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<Movie>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<SerieEpisode>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<SerieEpisode>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<Serie>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<Serie>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<SerieSeason>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<SerieSeason>>>();
        services.AddTransient<INotificationHandler<MediaPlaybackCompletedEvent<MusicAlbum>>, OutboundNotificationEventHandler<MediaPlaybackCompletedEvent<MusicAlbum>>>();

        services.AddScoped<IFederatedMediaResolver, FederatedMediaResolver>();
        services.AddScoped<IContentVisibilityEvaluator, ContentVisibilityEvaluator>();
        services.AddScoped<IFederationSocialPolicyService, FederationSocialPolicyService>();
        services.AddScoped<IUserFederationPrivacyService, UserFederationPrivacyService>();
        services.AddScoped<IVisibilityGrantService, VisibilityGrantService>();
        services.AddScoped<IFederationSocialConsumerService, FederationSocialConsumerService>();
        services.AddScoped<ISocialUserProfileService, SocialUserProfileService>();
        services.AddSingleton<IFederationViewerAssertionService, FederationViewerAssertionService>();

        return services;
    }
}
