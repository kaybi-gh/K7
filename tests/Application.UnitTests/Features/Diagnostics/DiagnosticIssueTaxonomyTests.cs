using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;
using K7.Shared.Dtos.Diagnostics;

namespace K7.Server.Application.UnitTests.Features.Diagnostics;

[TestFixture]
public class DiagnosticIssueTaxonomyTests
{
    [Test]
    public void Canonicalize_ShouldMapUnidentifiedFile_ToOrphanFile()
    {
        DiagnosticIssueTaxonomy.Canonicalize(DiagnosticIssue.UnidentifiedFile)
            .Should().Be(DiagnosticIssue.OrphanFile);
    }

    [Test]
    public void Canonicalize_ShouldLeaveSurfacedIssuesUnchanged()
    {
        DiagnosticIssueTaxonomy.Canonicalize(DiagnosticIssue.OrphanFile)
            .Should().Be(DiagnosticIssue.OrphanFile);
        DiagnosticIssueTaxonomy.Canonicalize(DiagnosticIssue.MissingMembers)
            .Should().Be(DiagnosticIssue.MissingMembers);
    }

    [Test]
    public void IsSurfaced_ShouldHideMissingFilesAndUnidentifiedFile()
    {
        DiagnosticIssueTaxonomy.IsSurfaced(DiagnosticIssue.MissingFiles).Should().BeFalse();
        DiagnosticIssueTaxonomy.IsSurfaced(DiagnosticIssue.UnidentifiedFile).Should().BeFalse();
        DiagnosticIssueTaxonomy.IsSurfaced(DiagnosticIssue.OrphanFile).Should().BeTrue();
        DiagnosticIssueTaxonomy.IsSurfaced(DiagnosticIssue.MissingMembers).Should().BeTrue();
    }

    [Test]
    public void GetOrphanRowSeverity_ShouldAlwaysBeError()
    {
        DiagnosticIssueTaxonomy.GetOrphanRowSeverity(hasIdentification: true)
            .Should().Be(DiagnosticSeverity.Error);
        DiagnosticIssueTaxonomy.GetOrphanRowSeverity(hasIdentification: false)
            .Should().Be(DiagnosticSeverity.Error);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.OrphanFile)
            .Should().Be(DiagnosticSeverity.Error);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.UnidentifiedFile)
            .Should().Be(DiagnosticSeverity.Error);
    }

    [Test]
    public void GetSeverity_ShouldMatchRequalifiedBands()
    {
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.InaccessiblePath)
            .Should().Be(DiagnosticSeverity.Error);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.MissingExternalId)
            .Should().Be(DiagnosticSeverity.Error);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.DuplicateExternalId)
            .Should().Be(DiagnosticSeverity.Warning);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.MissingMetadata)
            .Should().Be(DiagnosticSeverity.Info);
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssue.MissingMembers)
            .Should().Be(DiagnosticSeverity.Info);
    }
}

[TestFixture]
public class LibraryHealthSummaryCountsTests
{
    [Test]
    public void CountIssue_ShouldReturnZero_ForMissingFiles_AndCanonicalizeUnidentifiedToOrphan()
    {
        var summary = CreateSummary(
            orphan: 5,
            identifiedOrphan: 2,
            unidentified: 3,
            mediaWithoutFiles: 9,
            missingMembers: 4);

        LibraryHealthSummaryCounts.SumIssue([summary], DiagnosticIssue.MissingFiles).Should().Be(0);
        // UnidentifiedFile filters alias to OrphanFile (merged count), not the raw unidentified field.
        LibraryHealthSummaryCounts.SumIssue([summary], DiagnosticIssue.UnidentifiedFile).Should().Be(5);
        LibraryHealthSummaryCounts.SumIssue([summary], DiagnosticIssue.OrphanFile).Should().Be(5);
        LibraryHealthSummaryCounts.SumIssue([summary], DiagnosticIssue.MissingMembers).Should().Be(4);
    }

    [Test]
    public void SumErrors_ShouldCountAllOrphans_NotMediaWithoutFiles()
    {
        var summary = CreateSummary(
            orphan: 5,
            identifiedOrphan: 2,
            unidentified: 3,
            missingFileMetadata: 1,
            missingExternalId: 4,
            inaccessiblePath: 2,
            mediaWithoutFiles: 99);

        LibraryHealthSummaryCounts.SumErrors([summary]).Should().Be(5 + 1 + 4 + 2);
    }

    [Test]
    public void SumWarnings_ShouldOnlyCountDuplicateExternalId()
    {
        var summary = CreateSummary(
            unidentified: 3,
            inaccessiblePath: 1,
            duplicateExternalId: 2,
            missingMetadata: 4,
            missingHls: 50,
            missingPictures: 50);

        LibraryHealthSummaryCounts.SumWarnings([summary]).Should().Be(2);
    }

    [Test]
    public void SumInfo_ShouldIncludeMissingMembersMetadataAndPolishIssues()
    {
        var summary = CreateSummary(
            missingHls: 1,
            missingChapters: 2,
            missingTheme: 3,
            missingIntro: 4,
            missingPictures: 5,
            stale: 6,
            missingAudio: 7,
            missingMembers: 8,
            suspectedDuplicate: 9,
            missingMetadata: 10);

        LibraryHealthSummaryCounts.SumInfo([summary]).Should().Be(1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9 + 10);
    }

    [Test]
    public void SumSeverity_ShouldCountAllOrphansInErrorBandOnly()
    {
        var summary = CreateSummary(
            orphan: 5,
            identifiedOrphan: 2,
            unidentified: 3,
            missingFileMetadata: 1,
            missingExternalId: 0,
            inaccessiblePath: 4,
            duplicateExternalId: 7,
            missingMetadata: 3);

        var context = new DiagnosticsFilterContext();
        var exclude = DiagnosticsFilterExclusions.Severity;

        LibraryHealthSummaryCounts.SumSeverity(
                [summary], LibraryHealthSummaryCounts.ErrorIssues, context, exclude)
            .Should().Be(5 + 1 + 4);

        LibraryHealthSummaryCounts.SumSeverity(
                [summary], LibraryHealthSummaryCounts.WarningIssues, context, exclude)
            .Should().Be(7);

        LibraryHealthSummaryCounts.SumSeverity(
                [summary], LibraryHealthSummaryCounts.InfoIssues, context, exclude)
            .Should().Be(3);
    }

    [Test]
    public void SumLibraryIssues_ShouldExcludeMissingFilesAndNotDoubleCountUnidentified()
    {
        var summary = CreateSummary(
            orphan: 5,
            identifiedOrphan: 2,
            unidentified: 3,
            missingFileMetadata: 1,
            mediaWithoutFiles: 99,
            missingMembers: 2);

        // Surfaced: Orphan(5) + MissingFileMetadata(1) + MissingMembers(2) = 8
        LibraryHealthSummaryCounts.SumLibraryIssues(summary).Should().Be(8);
    }

    [Test]
    public void CountEntityType_IndexedFile_ShouldCountMergedOrphanOnce()
    {
        var summary = CreateSummary(orphan: 5, unidentified: 3, missingFileMetadata: 1, missingHls: 2, missingChapters: 4);

        LibraryHealthSummaryCounts.SumEntityType([summary], DiagnosticEntityType.IndexedFile)
            .Should().Be(5 + 1 + 2 + 4);
    }

    [Test]
    public void SumWorkClass_ShouldSumSurfacedIssuesForClass()
    {
        var summary = CreateSummary(
            orphan: 5,
            missingFileMetadata: 1,
            inaccessiblePath: 2,
            duplicateExternalId: 3,
            suspectedDuplicate: 4,
            missingExternalId: 10);

        LibraryHealthSummaryCounts.SumWorkClass([summary], DiagnosticWorkClass.Catalog)
            .Should().Be(5 + 1 + 2 + 3 + 4);
    }

    private static LibraryHealthSummaryDto CreateSummary(
        int orphan = 0,
        int identifiedOrphan = 0,
        int unidentified = 0,
        int missingFileMetadata = 0,
        int missingHls = 0,
        int missingChapters = 0,
        int missingTheme = 0,
        int missingIntro = 0,
        int missingPictures = 0,
        int missingExternalId = 0,
        int missingMetadata = 0,
        int mediaWithoutFiles = 0,
        int stale = 0,
        int missingAudio = 0,
        int missingMembers = 0,
        int inaccessiblePath = 0,
        int duplicateExternalId = 0,
        int suspectedDuplicate = 0) => new()
    {
        LibraryId = Guid.NewGuid(),
        LibraryTitle = "Test",
        MediaType = LibraryMediaType.Movie,
        TotalMediaCount = 0,
        MediaMissingPicturesCount = missingPictures,
        MediaMissingExternalIdCount = missingExternalId,
        MediaMissingMetadataCount = missingMetadata,
        MediaWithoutFilesCount = mediaWithoutFiles,
        StaleMetadataCount = stale,
        MissingMembersCount = missingMembers,
        TotalIndexedFileCount = 0,
        OrphanIndexedFileCount = orphan,
        IdentifiedOrphanIndexedFileCount = identifiedOrphan,
        UnidentifiedIndexedFileCount = unidentified,
        MissingFileMetadataCount = missingFileMetadata,
        MissingHlsSegmentsCount = missingHls,
        MissingChaptersCount = missingChapters,
        MissingThemeSongCount = missingTheme,
        MissingIntroOutroCount = missingIntro,
        MissingAudioAnalysisCount = missingAudio,
        DuplicateExternalIdCount = duplicateExternalId,
        SuspectedDuplicateMediaCount = suspectedDuplicate,
        InaccessiblePathCount = inaccessiblePath,
        PendingBackgroundTaskCount = 0,
        FailedBackgroundTaskCount = 0
    };
}
