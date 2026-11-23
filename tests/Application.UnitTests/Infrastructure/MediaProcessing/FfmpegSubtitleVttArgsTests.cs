using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegSubtitleVttArgsTests
{
    [Test]
    public void Build_ShouldConvertOneStream_WithoutSeekOrTimestampOptions()
    {
        var args = FfmpegSubtitleVttArgs.Build(
            "/media/movie.mkv",
            [new KeyValuePair<int, string>(7, "/cache/7.vtt.tmp")]);

        args.Should().Be(
            "-hide_banner -loglevel error -nostdin -y -i \"/media/movie.mkv\""
            + " -map 0:7 -c:s webvtt -f webvtt \"/cache/7.vtt.tmp\"");
    }

    [Test]
    public void Build_ShouldWriteEveryStream_InASingleRun()
    {
        var args = FfmpegSubtitleVttArgs.Build(
            "/media/movie.mkv",
            [
                new KeyValuePair<int, string>(7, "/cache/7.vtt.tmp"),
                new KeyValuePair<int, string>(8, "/cache/8.vtt.tmp")
            ]);

        args.Should().Be(
            "-hide_banner -loglevel error -nostdin -y -i \"/media/movie.mkv\""
            + " -map 0:7 -c:s webvtt -f webvtt \"/cache/7.vtt.tmp\""
            + " -map 0:8 -c:s webvtt -f webvtt \"/cache/8.vtt.tmp\"");
    }

    [Test]
    public void Build_ShouldSeekTheInputAndKeepTheMediaTimeline_ForAWindow()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            var args = FfmpegSubtitleVttArgs.Build(
                "/media/movie.mkv",
                [new KeyValuePair<int, string>(7, "/cache/7.w30.vtt.tmp")],
                (TimeSpan.FromSeconds(3570), TimeSpan.FromSeconds(3720.5)));

            // -ss before -i (index seek), the end as an output option, and copyts with
            // start_at_zero so the cue times equal those of a full extraction.
            args.Should().Be(
                "-hide_banner -loglevel error -nostdin -y -ss 3570 -i \"/media/movie.mkv\""
                + " -copyts -start_at_zero"
                + " -map 0:7 -c:s webvtt -to 3720.5 -f webvtt \"/cache/7.w30.vtt.tmp\"");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }
}
