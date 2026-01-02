using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaFormatSampleGenerator : IMediaFormatSampleGenerator
{
    public async Task<MemoryStream> GenerateSampleAsync(BaseMediaFormat mediaFormat)
    {
        var memoryStream = new MemoryStream();

        var success = mediaFormat switch
        {
            AudioMediaFormat audioMediaFormat => await GenerateSilentSingleFrameAudioAsync(audioMediaFormat, memoryStream),
            VideoMediaFormat videoMediaFormat => await GenerateSilentSingleFrameVideoAsync(videoMediaFormat, memoryStream),
            _ => throw new NotImplementedException()
        };

        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    private static Task<bool> GenerateSilentSingleFrameAudioAsync(AudioMediaFormat audioMediaFormat, MemoryStream memoryStream)
    {
        return FFMpegArguments
            .FromFileInput("anullsrc=r=44100:cl=stereo", verifyExists: false, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .WithCustomArgument("-f lavfi -t 0.033"))
            .OutputToPipe(new StreamPipeSink(memoryStream), options => options
                .WithCustomArgument($"-f {audioMediaFormat.Container}")
                .WithAudioCodec(audioMediaFormat.Codec)
                .WithAudioBitrate(128)
                .WithFastStart())
            .ProcessAsynchronously();
    }

    private static Task<bool> GenerateSilentSingleFrameVideoAsync(VideoMediaFormat videoMediaFormat, MemoryStream memoryStream)
    {
        return FFMpegArguments
            .FromFileInput("color=c=black:s=16x16", verifyExists: false, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .WithCustomArgument("-f lavfi -t 0.033"))
            .AddFileInput("anullsrc=r=44100:cl=stereo", verifyExists: false, options => options
                .WithCustomArgument("-f lavfi -t 0.033"))
            .OutputToPipe(new StreamPipeSink(memoryStream), options =>
            {
                options.WithCustomArgument($"-f {videoMediaFormat.Container}")
                       .WithVideoCodec(videoMediaFormat.VideoCodec)
                       .WithConstantRateFactor(21)
                       .WithFastStart();

                if (!string.IsNullOrEmpty(videoMediaFormat.AudioCodec))
                {
                    options.WithAudioCodec(videoMediaFormat.AudioCodec)
                           .WithAudioBitrate(128);
                }
            })
            .ProcessAsynchronously();
    }
}
