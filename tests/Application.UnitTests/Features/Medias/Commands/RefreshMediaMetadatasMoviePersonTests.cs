using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class RefreshMediaMetadatasMoviePersonTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private IMetadataProvider<ExternalMovieMetadata> _movieProvider = null!;
    private ISender _sender = null!;
    private RefreshMediaMetadatasCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _movieProvider = Substitute.For<IMetadataProvider<ExternalMovieMetadata>>();
        _movieProvider.ProviderName.Returns("tmdb");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _movieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _handler = new RefreshMediaMetadatasCommandHandler(
            _context,
            _serviceProviderRoot,
            _sender,
            [],
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MetadataPictureDeletionService(
                _context,
                Substitute.For<ILogger<MetadataPictureDeletionService>>()),
            Substitute.For<ILogger<RefreshMediaMetadatasCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProviderRoot.Dispose();
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldQueuePersonRefreshOnlyForNewPersons_WhenMovieCastComesFromCredits()
    {
        var movie = new Movie { Title = "Film" };
        _context.Medias.Add(movie);
        _context.Persons.Add(new Person
        {
            Name = "Known Actor",
            Biography = "Already enriched",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "7" }]
        });
        await _context.SaveChangesAsync();

        _movieProvider.FetchMetadata(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalMovieMetadata
            {
                Title = "Film",
                PersonRoles =
                [
                    CreditActor(personTmdbId: 7, name: "Known Actor", creditId: "c1"),
                    CreditActor(personTmdbId: 42, name: "New Actor", creditId: "c2")
                ]
            });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = movie.Id,
            MetadataProviderExternalId = "603",
            MetadataProviderName = "tmdb",
            Language = "fr",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var queued = _sender.ReceivedCalls()
            .Select(call => call.GetArguments()[0])
            .OfType<CreateBackgroundTaskCommand>()
            .Select(command => command.Request)
            .OfType<RefreshPersonMetadataCommand>()
            .ToList();

        queued.Should().ContainSingle().Which.ProviderId.Should().Be("42");
    }

    private static Actor CreditActor(int personTmdbId, string name, string creditId) => new()
    {
        CharacterName = "Role",
        ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = creditId }],
        Person = new Person
        {
            Name = name,
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = personTmdbId.ToString() }]
        }
    };
}
