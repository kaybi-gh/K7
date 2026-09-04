# Playback bookmarks

Continue watching and resume position live in `PlaybackBookmarks`. Durable watch state stays on `UserMediaState` / `SharedProfileMediaState`.

## Why

`UserMediaState` tracks long-lived facts: completed, play/skip counts, last interaction. Resume position and next-episode hints are ephemeral UI state. Splitting them:

- Keeps media state slim for imports, merges, and shared-profile migration
- Lets dismiss remove continue-watching without touching watch history
- Supports series next-up even when the last activity is old, as long as the next episode is playable

## Entity model

`PlaybackBookmark` (TPH table `PlaybackBookmarks`):

| Kind | Type | Purpose |
|---|---|---|
| `Item` (0) | `ItemPlaybackBookmark` | Resume position for a movie or in-progress episode |
| `Series` (1) | `SeriesPlaybackBookmark` | Next episode after the user finished the previous one |

Ownership is either `UserId` or `SharedProfileId`, never both.

`UserMediaState` / `SharedProfileMediaState` keep: `IsCompleted`, `PlayCount`, `SkipCount`, `LastInteractedAt`.

`SeriesPlaybackBookmark` fields that drive Keep Watching age:

| Field | Role |
|---|---|
| `ActivityAt` | When the user last finished an episode |
| `NextEpisodeAvailableAt` | When the current next-up target was set (completion or scan) |

## Service

`IPlaybackBookmarkService` (`Application/Services/PlaybackBookmarkService.cs`):

| Method | Role |
|---|---|
| `UpsertItemBookmarkAsync` | Save resume position during playback |
| `RemoveItemBookmarkAsync` | Clear item bookmark (completed episode or manual clear) |
| `OnEpisodeCompletedAsync` | Upsert series bookmark, resolve next playable episode, remove bookmark if none |
| `RefreshSeriesBookmarksForSerieAsync` | After library scan / new episode, recalc `NextEpisodeId` for affected series bookmarks |
| `BackfillMissingNextEpisodesAsync` | Fill `NextEpisodeId` for series bookmarks left empty by migration |
| `ResolveNextPlayableEpisodeIdAsync` | Walk season/episode order, skip completed, require indexed or remote file |
| `DismissAsync` / `DismissForSharedProfileAsync` | Remove series bookmark (and episode item bookmarks) or a single item bookmark |
| `ExpireStaleSeriesBookmarksAsync` | Drop series bookmarks past max age when next episode was never started |
| `GetItemBookmarksAsync` | Batch load for DTO projection |

Registered in `Application/DependencyInjection.cs`.

## Eligibility (`ContinueWatchingEligibility`)

**Item bookmark** appears in Keep Watching when:

- `UpdatedAt` is within `ContinueWatchingMaxAgeDays` (if configured)
- Position meets `MinResumePercent` / `MinResumeDurationSeconds`
- Media has at least one indexed or remote file

**Series bookmark** appears when:

- `NextEpisodeId` is set and playable
- `NextEpisodeAvailableAt` is within the max-age window (`ActivityAt` alone does not hide next-up)
- No eligible in-progress episode bookmark for that series (item bookmark wins over next-up, even if the series bookmark was refreshed more recently)

### Product rules

- Caught up on season 1, then season 2 appears a year later: series returns to Keep Watching. The clock for ageing is `NextEpisodeAvailableAt`, not the old finish date.
- Season 2 appears but the user never starts it within the configured window: series leaves Keep Watching. Watch history and `IsCompleted` stay unchanged.
- Several new episodes arrive in one scan: only the first playable episode after the cursor is next-up.
- Mid-episode resume always beats series next-up for the same show. A library scan must not restart the in-progress episode from 00:00.

Dismiss removes bookmarks. It does not change `IsCompleted` on media state.

## Integration points

| Area | Behavior |
|---|---|
| `UpdatePlaybackProgress` | Updates item bookmark. Calls `OnEpisodeCompletedAsync` on completion |
| `SetMediaWatchState` | Bookmarks on watch/unwatch |
| `DismissFromContinueWatching` | `DismissAsync` |
| `BulkUpsertMediaStates` | Import writes item/series bookmarks (API unchanged for K7.Import) |
| `MediaCreatedEvent` (SerieEpisode) | `SeriesPlaybackBookmarkRefreshEventHandler` refreshes series bookmarks |
| `CreateMedia` / `ReidentifyIndexedFile` | `RefreshSeriesBookmarksForSerieAsync` when episode becomes playable |
| `HomeFeedContinueWatchingStrategy` | Queries `PlaybackBookmarks`, backfills missing next episodes, expires stale series bookmarks. In-progress episode bookmarks win over series next-up |
| `GetMedia` | Loads item bookmarks for the media graph and overlays them in `ToMediaDto` so Play/Resume receives `LastPlaybackPosition` |
| `LiteMediaProjectionService` | Merges item bookmarks into `UserMediaStateDto` for position/progress overlay. Series and season cards aggregate episode `IsCompleted` so the watched badge matches playback |
| Browse filters (`IsCompleted`, `InProgress`, `UnwatchedOnly`) | Series/season filters aggregate episode states. A series is completed only when every episode is completed |

## DTO mapping

`UserMediaStateMappings.ToUserMediaStateDto` accepts an optional `ItemPlaybackBookmark` to overlay `LastPlaybackPosition` and `ProgressPercentage` on the DTO without storing them on the entity. `GetMedia` and list projections must pass that bookmark; without it, resume position is always 0 and Play falls back to the first unwatched episode.

Series and season list items do not store their own `IsCompleted`. `LiteMediaProjectionService` and browse filters (`IsCompleted`, `InProgress`, `UnwatchedOnly`) aggregate episode states: a series or season is completed only when every episode is completed.

## Migration

`AddPlaybackBookmarks` (Postgres + Sqlite):

1. Creates `PlaybackBookmarks`
2. Copies in-progress states (not excluded, not completed) to item bookmarks
3. Copies latest completed episode per user/serie (or shared profile/serie) to series bookmarks
4. Drops progress/exclusion columns from media state tables

Series bookmarks migrated without `NextEpisodeId` are filled on the next Keep Watching load via `BackfillMissingNextEpisodesAsync`, or on the next library scan via `RefreshSeriesBookmarksForSerieAsync`.

## Tests

`PlaybackBookmarkServiceTests` (complete, dismiss, expire, late-season refresh, backfill, shared profile, batch next-up), `SeriesPlaybackBookmarkRefreshEventHandlerTests`, `ContinueWatchingEligibilityTests`, `ContinueWatchingFeedDeduperTests`, `MediaMappingsTests` (bookmark overlay), `GetMediaQueryHandlerTests`, and related handler tests in `tests/Application.UnitTests`.
