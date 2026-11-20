namespace K7.Server.Application.Helpers;

/// <summary>
/// Wall-clock budgets for MediaAnalysis-lane background tasks. Intro/outro detection
/// chromaprints every episode in a season and routinely exceeds the default 300s.
/// </summary>
public static class MediaAnalysisTimeouts
{
    public const int DetectMediaSegmentsSeconds = 3600;

    /// <summary>One full decode of an episode tail, at most a few minutes of video.</summary>
    public const int CreditsFrameSamplingSeconds = 300;
}
