# Architecture

K7 follows Clean Architecture with a strict dependency direction:

```text
Domain -> Application -> Infrastructure -> Web (host)
                              ^
Shared (DTOs) ----------------+
Clients (Blazor) consume Shared + HTTP/SignalR from Web
```

Setup, style, and PRs: [CONTRIBUTING.md](../../CONTRIBUTING.md). Day-to-day: [developing.md](developing.md). UI constraints: [design.md](design.md). Releases: [releasing.md](releasing.md). Agents: [AGENTS.md](../../AGENTS.md).

## Layer responsibilities

| Project | Namespace | Role | May reference |
|---|---|---|---|
| `Server/Domain` | `K7.Server.Domain` | Entities, value objects, enums, events, interfaces | Nothing |
| `Server/Application` | `K7.Server.Application` | CQRS use cases (MediatR), validators, mappings | Domain |
| `Server/Infrastructure/*` | `K7.Server.Infrastructure.*` | EF Core, filesystem, ffmpeg, external HTTP | Domain, Application |
| `Server/Web` | `K7.Server.Web` | ASP.NET Core host, Minimal APIs, SignalR, setup UI | All server layers |
| `Shared/K7.Shared` | `K7.Shared` | DTOs and constants shared with clients | Domain enums + `nameof` only |
| `Clients/Shared` | `K7.Clients.Shared` | Client services, models, interfaces | K7.Shared |
| `Clients/Shared/UI` | `K7.Clients.Shared.UI` | Blazor pages, layouts, K7 component library | Clients.Shared |
| `Clients/Web` / `Clients/MAUI` | ... | WASM and MAUI hosts | Clients.Shared + Shared.UI |
| `Clients/DesignSystem` | `K7.Clients.DesignSystem` | Component catalog (Blazor Server showcase) | Shared.UI |

## Request flow

```mermaid
sequenceDiagram
    participant Client
    participant Endpoint as Minimal API endpoint
    participant MediatR as ISender
    participant Handler as Command/Query handler
    participant Db as IApplicationDbContext

    Client->>Endpoint: HTTP
    Endpoint->>MediatR: Send(request)
    MediatR->>Handler: Handle
    Handler->>Db: EF Core
    Db-->>Handler: data
    Handler-->>Endpoint: response / exception
    Endpoint-->>Client: JSON or ProblemDetails
```

Pipeline behaviors (order): Validation -> Authorization -> UnhandledException -> Performance.

Exceptions map via `CustomExceptionHandler` (`ValidationException` 400, `ForbiddenAccessException` 403, `NotFoundException` 404). Handlers throw typed exceptions; no `Result<T>` wrapper.

CQRS layout: `Features/{Feature}/Commands|Queries/{Name}/{Name}.cs` (request + handler same file), validators alongside, domain event handlers under `EventHandlers/`. DTO mapping via extension methods in `Application/Common/Mappings/` (no AutoMapper). Endpoints in `Server/Web/Endpoints/` stay thin and delegate to `ISender`.

## OpenSubsonic compatibility layer

K7 exposes an OpenSubsonic-compatible facade under `/rest`, alongside the native `/api` used by first-party clients. Outbound Music intelligence calls go to AudioMuse's own HTTP API. When AudioMuse is configured as the mediaserver for K7, it reaches K7 through `/rest` with an OpenSubsonic `apiKey`.

| Concern | Behavior |
|---|---|
| IDs | Same Guid strings as `/api/medias/{id}` (no prefix) |
| Auth | `apiKey` query (extension `apiKeyAuthentication`) for admin/automation, or username + **app password** via `u`+`p` or Subsonic `t`+`s` (not the account password). API keys are not assigned to another user. Client name (`c`) registers an **External** device for admin devices, active streams, and history. |
| Implementation | `Application/Features/OpenSubsonic` + `Web/OpenSubsonic` |
| Streaming | Direct file when possible. Progressive ffmpeg transcode when `format` / `maxBitRate` requires it (`timeOffset` via `transcodeOffset`). History is written on `scrobble` / `reportPlayback`, not on `stream` alone. `reportPlayback` stopped uses the real position. Completion follows the effective K7 audio playback policy. Classic `scrobble(submission=true)` remains client-driven (full end). When a client starts another track without ending the previous one, K7 ends only the latest open session on that device at its known progress (no wall-clock backfill of older abandoned plays). Finished listens under 30s watched are flagged `IsSkipped` in playback history and increment `UserMediaState.SkipCount` (music player and Most skipped in watch stats). |
| Extensions | `apiKeyAuthentication`, `songLyrics`, `formPost`, `playbackReport`, `transcodeOffset` |
| Starred | Mapped to `UserRating` with value `> 5` (same store as star ratings in the K7 UI) |
| Play queue | Not implemented yet (`getPlayQueue` / `savePlayQueue` return not found). Planned alongside native K7 play-queue work. |
| Admin progress | External clients hide the active-stream progress bar until `reportPlayback` provides a timeline (`HasPlaybackProgress`). |

Out of scope for the facade: video, podcasts, internet radio, public shares, chat, jukebox, user admin via Subsonic, bookmarks, sonic-path OS extensions. Backlog: richer OS `transcoding` endpoints (`getTranscodeDecision` / `getTranscodeStream`).

## Infrastructure split

Under `src/Server/Infrastructure/`:

| Project | Responsibility |
|---|---|
| `Configuration` | Options types (`Database`, `Authentication`, `Security`, `Paths`) |
| `Database/Context` | EF Core, Identity, OpenIddict, interceptors, DI entry |
| `Database/Providers/Postgres` | Postgres migrations assembly |
| `Database/Providers/Sqlite` | Sqlite migrations assembly |
| `ExternalServices` | Outbound integrations (including federation HTTP client) |
| `FileSystem` | Ensure config/metadata/log/transcode directories exist |
| `MediaProcessing` | ffmpeg, metadata providers (TMDb, TVDB, MusicBrainz, ...) |

Migrations: always add for **both** providers - commands in [CONTRIBUTING.md](../../CONTRIBUTING.md#migrations).

## SignalR and transcoding

The Web host exposes a SignalR hub for remote control, Sync Play, and live UI updates (playback progress, user ratings, library changes). Proxies must allow WebSockets.

User ratings: `RateMedia` (and review upsert) broadcast `ReceiveUserRatingUpdated` to the acting user's hub group. Clients keep a session overlay (`IUserRatingSync`) so playlist/album/player `RatingStars` instances update immediately, including the originating session before the hub echo arrives.

Playback: client requests a stream decision -> remux vs transcode -> encoder selection (software or hardware) -> temp files under `Paths:Transcoding`. Details: [Operating - Transcoding](../admin/operating.md#transcoding).

## Federation (high level)

```mermaid
sequenceDiagram
    participant A as Server A admin
    participant SA as K7 A
    participant SB as K7 B
    participant B as Server B admin

    A->>SA: Request peer(remote BaseUrl)
    SA->>SB: POST peer-request
    B->>SB: Accept
    SB->>SA: Confirm + credentials
    SA->>SB: Discover remote libraries
```

Operator guide: [Operating - Federation](../admin/operating.md#federation). Social playback history is a live query of `MediaPlaybackSession` rows where the profile owner is the acting `UserId` (co-viewers are not federated), gated by `FederationContentType.PlaybackHistory` and the owner's Share privacy. Peers do not store a replica, so a personal delete (or last-participant shared delete) disappears on the next fetch. An actor opt-out that transfers `UserId` moves the federated row to the new actor. A co-viewer opt-out does not change federated history. Inbound peer stream logs (`GetFederationPlaybackHistory` / `StreamSessions`) are a separate surface.

## Domain events

Entities inherit `BaseEntity` and raise `BaseEvent` via `AddDomainEvent()`. EF Core interceptors dispatch events after save. Non-EF paths (e.g. transcode failures, AudioMuse health) use `IDomainEventPublisher`.

## Shared profiles

`SharedProfile` is a membership hat over real `User` accounts (not a fake user). When active (client sends `X-Shared-Profile-Id`, validated server-side against membership), continue watching uses profile-scoped `PlaybackBookmarks` (and durable watched flags on `SharedProfileMediaStates`), and history/stats queries for that session are scoped to sessions tagged with the profile. Personal mid-progress is not written to personal bookmarks or `UserMediaState` (so personal continue watching stays clean) - any member (host or co-viewer) can resolve and drive a shared session. Resume position and series next-up live in `PlaybackBookmarks` owned by the shared profile, not as fake 0% media states. Detail: [playback-bookmarks.md](playback-bookmarks.md). `MediaPlaybackSession` rows still record the acting `UserId` with `SharedProfileId` set and list the other members as co-viewers; personal history and watch stats include those sessions for every member. When a shared session newly completes, each member's personal `UserMediaState` is marked watched for that media (completed only - no personal CW pollution). A member with the `CanReassignHistory` capability (default on for Admin only, off for User/Guest. Admin overrides via `UserCapabilityOverride`) can later reassign a history item (`PUT /api/stats/history/{referenceId}/assignment`) between personal and a shared profile they belong to (host can also reassign another member's play on a hosted profile): the command retags `MediaPlaybackSessions`, maintains co-viewers, and updates `SharedProfileMediaStates` / personal continue-watching to match. A member can also delete a history item (`DELETE /api/stats/history/{referenceId}`) when they have the `CanDeleteHistory` capability (same defaults): a personal play removes the sessions and decrements that user's play/skip counts. A shared-profile play only opts the current user out (drop co-viewer or transfer the acting `UserId` to a remaining co-viewer) without changing other members' personal counts or the profile's `SharedProfileMediaState`. If nobody remains on that play, the sessions are removed and only the shared profile counts are adjusted. The host can still remove a shared play they did not join, without un-watching other members. An administrator can do the same for any row from Admin -> Playback history (`DELETE /api/admin/stats/history/{referenceId}`): personal plays are fully deleted (counts of that user only). Shared-profile plays remove the group sessions without decrementing members' personal counts. Admin reassign (`PUT /api/admin/stats/history/{referenceId}/assignment`) can target any shared profile and still requires `CanReassignHistory`. When a shared profile is deleted (including leave that removes the last viable membership), shared media states are merged into each member's personal `UserMediaState` before cascade delete. Playback policy settings (`SharedProfileSettings`), the optional content restriction profile (`SharedProfile.ContentRestrictionProfileId`), and shared playlists (`SharedProfilePlaylists`) are owned by the profile itself, not the host personally: `MediaAccessGuard` and the playback policy provider prefer the active shared profile's own settings over the acting member's personal ones, and a shared profile with no restriction profile assigned means unrestricted for that session (no fallback to a member's personal profile). Person `GetPersonKnownFor` follows the same resolution and returns no external posters when a restriction profile is in effect, because those TMDB images are not in the library and cannot be evaluated against restriction rules. An optional profile avatar is stored as `MetadataPicture` (`SharedProfileAvatar`) linked via `SharedProfileId`; when set, clients show a single `K7Avatar` instead of the stacked member `K7AvatarGroup`. Native clients keep a device-wide shared-profile cache merged by id across sign-ins, so a profile pinned with **Show on this device** stays on the profile selection screen even after a local user who is not a member signs in.

## UI layout

| Area | Path |
|---|---|
| Pages | `src/Clients/Shared/UI/Pages/` |
| Layouts | `src/Clients/Shared/UI/Layout/` |
| K7 components | `src/Clients/Shared/UI/Components/` |
| Client services | `src/Clients/Shared/Services/` |
| Tokens / themes | `src/Clients/Shared/UI/wwwroot/` |
| DesignSystem catalog | `src/Clients/DesignSystem/` |

**Triad:** `.razor` + `.razor.cs` + optional `.razor.css`. Put logic in `.razor.cs`; keep `@code` only for tiny leaves (about 15 lines or fewer of parameters/no methods). Never leave both a non-trivial `@code` block and a `.razor.cs` on the same component. No third-party UI frameworks in pages. Theming and visual rules: [design.md](design.md). Localization and DesignSystem workflow: [developing.md](developing.md).

Native clients keep Home mounted across navigation (`FeedHub`). `HomeFeedStore` is a process singleton scoped by identity user id plus the optional active shared profile id. Switching a local user (or shared profile) on MAUI reloads the home rows and continue-watching cache instead of reusing the previous feed. The SignalR hub reconnects so playback-progress events follow the new identity.

Movie and series trailers (TMDb / TVDB YouTube ids) play in a fullscreen opaque overlay by default. The overlay has no `backdrop-filter` or ancestor `transform`, so the browser can hardware-decode the iframe. `VideoPlayerSettingsDto.OpenTrailersExternally` (user override of the server default) sends YouTube to the system app on native clients and TV instead. Web desktop/phone always stay in the overlay. Non-embeddable sites always open externally.

MAUI `BlazorWebView.StartPath` opens `/select-profile` (or `/welcome` when no local users, `/linkdevice` on TV when Guest is disabled, or `/` for solo auto-login) so `MainLayout` / FeedHub do not run around `RedirectToLogin`. Video.js and audio player scripts are deferred until after first paint on Windows / Web (`PlaybackAssetLoader`). Play awaits them if the prefetch has not finished.

## Video playback (MAUI)

During video play on Android/iOS/Windows, MAUI uses a **native XAML overlay** on top of the decode surface (ExoPlayer / MediaElement on Android, MediaElement on iOS, LibVLC on Windows Direct Play. Windows HLS uses Video.js in WebView2 under the same native chrome). Web WASM stays Video.js + Blazor controls. Browse UI stays Blazor Hybrid. Details: [video-playback.md](video-playback.md).

## Music playback (MAUI Android)

Android music uses two Media3 ExoPlayers in `K7MediaLibraryService` (session player + idle player) so crossfade and gapless can overlap. After the blend, the incoming player is promoted in place (`ForwardingSimpleBasePlayer.setPlayer`). The next track is not reloaded onto the freed session player. Android Auto skip uses the ExoPlayer playlist as source of truth (same idea as Tempus / Jellyfin). Next/previous seek the Media3 timeline, then `IAudioPlayerService` copies the current index without reloading the stream. Car radios still fast-start on a small first batch so playback is not blocked on a large fetch. Refill then appends to the Media3 playlist. Windows music stays on WebView2 / Web Audio. iOS uses dual `AVPlayer`.

## Offline downloads (MAUI)

Offline transfers run in-process via `DownloadManager` (`HttpClient` streaming). The queue is in-memory: force-stop or process death still loses in-progress transfers (completed files persist in SQLite + `AppData/downloads`).

Offline playback progress and ratings are queued in `PendingPlaybackEvents` with `IdentityUserId`. `PlaybackSyncService` flushes them only after first Blazor paint (past select-profile / splash) for the currently **online** authenticated user. Rows without a user id are dropped and never sent.

On Android, a `dataSync` foreground service (`DownloadForegroundService`) with an ongoing notification keeps the process alive while **user** downloads are queued, preparing, or transferring. Music-cache lookahead does not start that service (playback already has a media foreground service). Other MAUI platforms have no equivalent keep-alive yet.
