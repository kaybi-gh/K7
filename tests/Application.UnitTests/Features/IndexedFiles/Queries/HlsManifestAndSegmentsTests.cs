using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Queries;

[TestFixture]
public class HlsManifestAndSegmentsTests
{
    [Test]
    public void SerializeAndDeserializeAudioTrackTranscodings_ShouldRoundTrip()
    {
        var map = new Dictionary<int, string> { [0] = "aac", [2] = "opus" };

        var serialized = GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StreamSessionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AudioTrackTranscodings = map,
            TranscodingVideoCodec = "h264"
        });

        serialized.Should().Contain("0%3Aaac");
        serialized.Should().Contain("2%3Aopus");
        serialized.Should().Contain("TranscodingVideoCodec=h264");

        var restored = GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings("0:aac,2:opus");
        restored.Should().Equal(map);
    }

    [Test]
    public void DeserializeAudioTrackTranscodings_ShouldReturnNull_ForBlank()
    {
        GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings(null).Should().BeNull();
        GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings(" ").Should().BeNull();
    }

    [Test]
    public async Task GetHlsSegments_ShouldReturnOrderedSegmentsForIndexedFile()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var indexedFileId = Guid.NewGuid();
        var otherFileId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var otherMetadataId = Guid.NewGuid();

        context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Lib",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        context.IndexedFiles.AddRange(
            new IndexedFile
            {
                Id = indexedFileId,
                LibraryId = libraryId,
                Name = "movie",
                Extension = ".mkv",
                Path = "/media/movie.mkv",
                Hash = 1,
                Size = 1,
                FileMetadata = new VideoFileMetadata
                {
                    Id = metadataId,
                    Container = "matroska",
                    VideoBitrate = 1,
                    VideoResolution = VideoResolutionIdentifier._1080p
                }
            },
            new IndexedFile
            {
                Id = otherFileId,
                LibraryId = libraryId,
                Name = "other",
                Extension = ".mkv",
                Path = "/media/other.mkv",
                Hash = 2,
                Size = 1,
                FileMetadata = new VideoFileMetadata
                {
                    Id = otherMetadataId,
                    Container = "matroska",
                    VideoBitrate = 1,
                    VideoResolution = VideoResolutionIdentifier._720p
                }
            });
        await context.SaveChangesAsync();

        context.HlsSegments.AddRange(
            new HlsSegment { FileMetadataId = metadataId, IndexedFileId = indexedFileId, Number = 2, StartTimestamp = 20, Duration = 10 },
            new HlsSegment { FileMetadataId = metadataId, IndexedFileId = indexedFileId, Number = 1, StartTimestamp = 0, Duration = 10 },
            new HlsSegment { FileMetadataId = otherMetadataId, IndexedFileId = otherFileId, Number = 1, StartTimestamp = 0, Duration = 10 });
        await context.SaveChangesAsync();

        var handler = new GetHlsSegmentsQueryHandler(context);
        var segments = await handler.Handle(new GetHlsSegmentsQuery { IndexedFileId = indexedFileId }, CancellationToken.None);

        segments.Should().HaveCount(2);
        segments.Select(s => s.Number).Should().Equal(1, 2);
    }
}
