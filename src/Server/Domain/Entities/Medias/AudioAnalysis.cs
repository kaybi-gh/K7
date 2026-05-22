namespace K7.Server.Domain.Entities.Medias;

/// <summary>
/// Stores audio analysis results computed from the actual audio signal (not file tags).
/// Populated asynchronously by background analysis tasks using tools like Chromaprint/Essentia.
/// One-to-one optional relationship with MusicTrack.
/// </summary>
public class AudioAnalysis : BaseAuditableEntity
{
    public Guid MusicTrackId { get; set; }
    public MusicTrack MusicTrack { get; set; } = null!;

    // Chromaprint: perceptual fingerprint of the audio signal.
    // Two different encodings of the same song produce the same fingerprint.
    // Used for duplicate detection and AcoustID identification.
    public string? ChromaprintFingerprint { get; set; }
    public int? ChromaprintDurationSeconds { get; set; }

    // AcoustID: online service that maps fingerprints to MusicBrainz recordings.
    // Score indicates match confidence (0.0 = no match, 1.0 = perfect match).
    public string? AcoustId { get; set; }
    public double? AcoustIdScore { get; set; }

    // Tempo/rhythm analysis (detected from the signal, not file tags).
    public double? Bpm { get; set; }

    // Musical key detected from harmonic analysis (e.g. "C major", "A minor").
    // Uses Camelot or Open Key notation for DJ-friendly compatibility.
    public string? MusicalKey { get; set; }

    // Loudness in LUFS (Loudness Units Full Scale). Industry standard measurement.
    // Typical values: -14 LUFS (streaming target), -6 LUFS (very loud master).
    public double? LoudnessLufs { get; set; }

    // Loudness range in LU - how much dynamic variation the track has.
    // Low (< 5 LU) = compressed/loud, High (> 10 LU) = dynamic/classical.
    public double? LoudnessRange { get; set; }

    // Energy: overall perceived intensity (0.0 = ambient, 1.0 = extreme).
    // Computed from RMS, spectral flux, and onset rate.
    public double? Energy { get; set; }

    // Danceability: how suitable the track is for dancing (0.0 to 1.0).
    // Based on tempo stability, beat strength, and rhythmic regularity.
    public double? Danceability { get; set; }

    // Valence: musical positivity (0.0 = sad/dark, 1.0 = happy/bright).
    // Inferred from mode (major/minor), tempo, and harmonic content.
    public double? Valence { get; set; }

    // Normalized amplitude peaks (0.0-1.0) sampled across the track duration.
    // Used to render waveform visualization in the player UI.
    public float[]? WaveformPeaks { get; set; }

    // MixRamp: time (seconds) from track start until audio reaches threshold dB.
    // Used for sweet fades - overlapping transitions during quiet portions.
    public double? FadeInDuration { get; set; }

    // MixRamp: time (seconds) before track end where audio drops below threshold dB.
    // The crossfade can begin at this point for a musically-aware transition.
    public double? FadeOutDuration { get; set; }

    // ReplayGain track gain (dB) parsed from file tags. Fallback for loudness normalization
    // when LoudnessLufs is not yet computed from signal analysis.
    public double? ReplayGainTrackGain { get; set; }

    // ReplayGain album gain (dB) parsed from file tags.
    public double? ReplayGainAlbumGain { get; set; }

    // When the analysis was performed. Null if not yet analyzed.
    public DateTime? AnalyzedAt { get; set; }

    // Version of the analysis pipeline. Allows re-analysis when algorithms improve.
    public int AnalysisVersion { get; set; }
}
