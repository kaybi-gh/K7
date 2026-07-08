using System.Net;
using K7.Server.Application;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Application.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockQueryable.NSubstitute;

namespace K7.Server.Application.UnitTests.Features.MetadataPictures.Commands;

[TestFixture]
public class DownloadMetadataPictureFromProviderCommandHandlerTests
{
    private IApplicationDbContext _context;
    private IHttpClientFactory _httpClientFactory;
    private IImageProcessor _imageProcessor;
    private ISender _sender;
    private MetadataPictureDeletionService _pictureDeletionService;
    private IBackgroundTaskExecutionContext _taskExecutionContext;
    private ILogger<DownloadMetadataPictureFromProviderCommandHandler> _logger;
    private DownloadMetadataPictureFromProviderCommandHandler _handler;
    private List<MetadataPicture> _pictures;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _imageProcessor = Substitute.For<IImageProcessor>();
        _sender = Substitute.For<ISender>();
        _logger = Substitute.For<ILogger<DownloadMetadataPictureFromProviderCommandHandler>>();
        _pictures = [];

        var dbSet = _pictures.BuildMockDbSet();
        _context.MetadataPictures.Returns(dbSet);

        _pictureDeletionService = new MetadataPictureDeletionService(
            _context,
            Substitute.For<ILogger<MetadataPictureDeletionService>>());

        var paths = Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() });

        _taskExecutionContext = new BackgroundTaskExecutionContext();

        _handler = new DownloadMetadataPictureFromProviderCommandHandler(
            _context,
            _httpClientFactory,
            _imageProcessor,
            _sender,
            paths,
            new OutboundRateLimiter(),
            new MediaPictureReadyNotifier(
                _context,
                Substitute.For<ILibraryNotifier>(),
                Substitute.For<ILogger<MediaPictureReadyNotifier>>()),
            _pictureDeletionService,
            _taskExecutionContext,
            _logger);
    }

    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.Forbidden)]
    public async Task Handle_ShouldRemovePicture_WhenRemoteImageIsUnavailable(HttpStatusCode statusCode)
    {
        var pictureId = Guid.NewGuid();
        var picture = new MetadataPicture
        {
            Id = pictureId,
            MediaId = Guid.NewGuid(),
            Type = MetadataPictureType.Still,
            OriginalRemoteUri = new Uri("https://artworks.thetvdb.com/banners/episodes/1/2.jpg")
        };
        _pictures.Add(picture);

        var httpClient = new HttpClient(new StubHttpMessageHandler(statusCode));
        _httpClientFactory.CreateClient(DependencyInjection.MetadataPictureDownloadClient).Returns(httpClient);

        await _handler.Handle(new DownloadMetadataPictureFromProviderCommand { Id = pictureId }, CancellationToken.None);

        _context.MetadataPictures.Received(1).Remove(picture);
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _taskExecutionContext.IsCancelled.Should().BeTrue();
        _taskExecutionContext.CancellationDetails.Should().Contain(((int)statusCode).ToString());
        await _sender.DidNotReceive().Send(Arg.Any<IRequest>(), Arg.Any<CancellationToken>());
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
