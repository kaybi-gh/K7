using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.Common.Mappings;

public static class FileMetadataMappings
{
    extension(BaseFileMetadata domain)
    {
        public FileMetadataDto ToFileMetadataDto() => domain switch
        {
            AudioFileMetadata audioFileMetadata => new AudioFileMetadataDto()
            {
                Id = domain.Id,
                Container = domain.Container,
                Duration = audioFileMetadata.Duration,
                AudioTrack = audioFileMetadata.AudioTrack?.ToAudioFileTrackDto()
            },
            VideoFileMetadata videoFileMetadata => new VideoFileMetadataDto()
            {
                Id = domain.Id,
                Container = domain.Container,
                Duration = videoFileMetadata.Duration,
                AudioTracks = videoFileMetadata.AudioTracks.Select(t => t.ToAudioFileTrackDto()).ToList(),
                VideoTracks = videoFileMetadata.VideoTracks.Select(t => t.ToVideoFileTrackDto()).ToList(),
                SubtitleTracks = videoFileMetadata.SubtitleTracks.Select(t => t.ToSubtitleFileTrackDto()).ToList(),
                VideoBitrate = videoFileMetadata.VideoBitrate,
                VideoResolution = videoFileMetadata.VideoResolution,
                Thumbnails = videoFileMetadata.Thumbnails?.ToMetadataPictureDto()
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };
    }

    extension(BaseFileTrack domain)
    {
        public FileTrackDto ToFileTrackDto() => domain switch
        {
            AudioFileTrack audioFileTrack => audioFileTrack.ToAudioFileTrackDto(),
            VideoFileTrack videoFileTrack => videoFileTrack.ToVideoFileTrackDto(),
            SubtitleFileTrack subtitleFileTrack => subtitleFileTrack.ToSubtitleFileTrackDto(),
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };
    }

    extension(AudioFileTrack domain)
    {
        public AudioFileTrackDto ToAudioFileTrackDto() => new()
        {
            Index = domain.Index,
            IsDefault = domain.IsDefault,
            Name = domain.Name,
            Language = domain.Language,
            Codec = domain.Codec,
            Channels = domain.Channels,
            ChannelLayout = domain.ChannelLayout,
            SampleRateHz = domain.SampleRateHz,
            Profile = domain.Profile
        };
    }

    extension(VideoFileTrack domain)
    {
        public VideoFileTrackDto ToVideoFileTrackDto() => new()
        {
            Index = domain.Index,
            IsDefault = domain.IsDefault,
            BitDepth = domain.BitDepth,
            Codec = domain.Codec,
            Height = domain.Height,
            Level = domain.Level,
            PixelFormat = domain.PixelFormat,
            Profile = domain.Profile,
            Width = domain.Width
        };
    }

    extension(SubtitleFileTrack domain)
    {
        public SubtitleFileTrackDto ToSubtitleFileTrackDto() => new()
        {
            Index = domain.Index,
            IsDefault = domain.IsDefault,
            Name = domain.Name,
            Language = domain.Language,
            Codec = domain.Codec,
            IsTextBased = domain.IsTextBased,
            IsForced = domain.IsForced
        };
    }

    extension(BaseMediaFormat domain)
    {
        public MediaFormatDto ToMediaFormatDto() => domain switch
        {
            AudioMediaFormat audioMediaFormat => new AudioMediaFormatDto()
            {
                Id = domain.Id,
                Container = domain.Container,
                Codec = audioMediaFormat.Codec
            },
            VideoMediaFormat videoMediaFormat => new VideoMediaFormatDto()
            {
                Id = domain.Id,
                Container = domain.Container,
                AudioCodec = videoMediaFormat.AudioCodec,
                VideoCodec = videoMediaFormat.VideoCodec
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };
    }
}
