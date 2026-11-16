# Public share links

A public share is a temporary link that lets an anonymous visitor browse, play and download one media and everything below it. This page explains how the feature stays contained. User and admin views: [user guide](../user/guide.md#public-share-links), [operating](../admin/operating.md#public-share-links).

## Scope

A share has one root media. It covers the root and its descendants, never its parents or siblings.

| Root | Covers |
|---|---|
| Movie, episode, track | That item only |
| Season | The season and its episodes |
| Series | The series, its seasons and their episodes |
| Album | The album and its tracks |
| Artist | The artist, their albums and those albums' tracks |

`IShareScopeResolver` computes the scope live from the existing keys (`SeasonId`, `SerieId`, `AlbumId`, `ArtistId`), so an episode added later to a shared series is included. An artist share follows the album artist: a track only credited on someone else's album is out of scope. Media hosted on a peer server cannot be shared.

A visitor sees parents as plain text (series name on an episode page) but cannot open them. The cast carousel is shown too, with `PersonRole Linked="false"`: person pages are not part of a share, so the cards are not links.

## Entities

- `MediaShare`: root media, hashed link token, optional password hash, expiration, access limit and counter, playback / download flags, `CloseWhenConsumed`, `RevokedAt`, creator.
- `MediaShareSession`: one visitor that opened the share. It counts as one access and holds a hashed session token, IP, user agent and the technical device used for stream negotiation.

Tokens are 256 random bits. Only their SHA-256 is stored, so a link cannot be shown again: **Generate a new link** replaces it and drops the visitor sessions.

`MediaShare.GetStatus` gives `Active`, `Expired`, `Exhausted`, `Consumed` or `Revoked`. `Suspended` is added by `MediaShareManager.ResolveStatus` when the creator may no longer share.

- Reaching the access limit refuses new visitors. Visitors already in keep their session.
- Expiration and revocation cut everyone.
- A password is locked for `MediaShareRules.PasswordLockDuration` after `MaxPasswordAttempts` wrong tries in a row (`FailedPasswordAttempts`, `PasswordLockedUntil`). The per-IP rate limit alone is not enough: the link holder is the only one who can try passwords.
- Setting a new password drops every visitor session, so everyone is asked again.
- A consumed share refuses new visitors and keeps current ones for `MediaShare.ConsumedGracePeriod`.

## Who can share

Capability `CanShareMedia`, on by default for administrators only, grantable per user (Admin -> Users -> capabilities). The server switch `ServerFeatureFlagsDto.PublicSharesEnabled` is off by default: while off, no link opens and none can be created.

Rules enforced by the handlers (`Features/MediaShares`):

- A user can only share a media they can access.
- A visitor never gets more than the creator has now: the access guard re-checks the creator's exclusions, restriction profile and age gate on every media.
- Shares of a creator who is deactivated or lost the capability are suspended.
- Users manage their own shares. Administrators manage every share. An owner can always revoke or delete, even without the capability.

## How a visitor is authenticated

1. The link is `/share#<token>`. The token sits in the URL fragment, which browsers never send to a server, so it stays out of request logs.
2. The page posts the token in the body of `POST /api/public/shares/open` (anonymous, rate limited by IP). The handler checks status and password, increments the access counter atomically, and creates a `MediaShareSession`.
3. The response sets the `K7.Shares` cookie (HttpOnly, path `/api`, Secure unless `Security:ForceHttps` is off). It holds one session token per open share, so several shares can be open in one browser.
4. The page then replaces its address with `/share/{shareId}/m/{rootMediaId}`: the token leaves the address bar and the browser history.
5. `ShareSessionAuthenticationHandler` (scheme `ShareSession`) turns the cookie into a principal acting as the technical account `SystemUsers.ShareVisitorUserName`, with role `ShareVisitor` and one grant claim per share.

Validated cookies and stream scope decisions are cached for a few seconds. Their cache keys carry `IShareAccessInvalidator.Version`, bumped on revoke, edit, delete, new link and when the server switch changes, so those take effect at once. Only a natural expiration waits for the cache (15 seconds at most). Refusals are never cached.

A returning visitor with a valid session reopens the share without a password and without a new access.

## Containment

Three layers keep a visitor inside the share:

1. **Role.** `ShareVisitor` is in no regular policy (`GuestOrAbove`, `UserOrAbove`, `AdminOnly`). The scheme is listed only by `Policies.ShareAccess` (the `/api/public/shares/{shareId}/...` endpoints) and `Policies.StreamAccess`. Everything else answers 401.
2. **Stream scope requirement.** `StreamAccess` carries `ShareStreamScopeRequirement`. For a visitor it reads the file id from the route (`{id}`) and requires a share that covers the file and allows playback. A stream route without that parameter is refused for visitors.
3. **Access guard.** `MediaAccessGuard` asks `IShareVisitorAccess` first. A visitor never falls through to the regular user rules (which let a request without a user id through).

A request can carry a real account and a share cookie together (the stream policy lists both schemes). The real account always wins: `IShareVisitorContext.IsShareVisitor` is false as soon as a regular role is present, and `CurrentUser.IdentityId` ignores the share identity when another one exists. Without that, a restricted user holding any share cookie would be checked as the unrestricted visitor account.

Quality and codec names of the stream routes name the transcode output directory. `TranscodeJobManager` only accepts plain tokens for them and checks the directory stays under the transcoding folder: these routes are reachable by anonymous visitors.

`GetSharedMediaQuery` reuses `GetMediaQuery`, then removes what a visitor must not see: children the creator cannot access (exclusions, restriction profile, age gate), files on peer servers, user ratings and state, the library id and disk locations (only the file name is kept).

### Known limits

- **Download off is not copy protection.** Playback serves the media (HLS segments, or the original file for audio that plays directly), so a determined visitor can save it. The option removes the download button and the attachment endpoint.
- **Close once consumed** relies on the position sent by the visitor browser.
- **No cap on concurrent transcodes per share.** Creating stream sessions is rate limited per visitor (`share-stream`, 30 per minute), not per share.
- **Rate limits use the connection address.** Behind a reverse proxy every visitor shares the proxy address for the `share-open` bucket (same as the sign-in limit), and the visitor IP shown to owners is the proxy. Forwarding the client address is a server-wide choice left to the administrator.

## Playback and download

The public pages use the regular players (video player with audio tracks, subtitles, quality and speed; music player with its queue), although a visitor has no account, no registered device and no hub:

- `ISharePlaybackScope` is set by `ShareLayout` (visitor page) and by `SharePage` (open share).
- `ShareAwareStreamUriService` wraps `IStreamUriService`: while a share is open, stream sessions are created through `POST /api/public/shares/{shareId}/stream-sessions`.
- `ShareVisitorHttpHandler` (web client) answers every other call of the signed-in API client locally with an empty result while a visitor page is on screen. The players ask for settings, intro markers or the next episode: those calls never reach the server and never trigger the sign-in redirect. This is a UX filter, not a security control: the server refuses them anyway.
- Account features are hidden in the players on a visitor page (track menu, playlists, play on device, catalog links). The music queue only holds tracks of the page: no radio, no similar tracks.
- The page probes the browser (containers, codecs, decoder profiles) and sends the raw result. The server matches it against the format catalog and stores it on a technical `Device` attached to the visitor session, then runs the regular negotiation (`StreamSessionNegotiator`, shared with `POST /api/stream-sessions`).
- Segments are served by the regular stream endpoints, authenticated by the cookie.
- Seek bar previews use the thumbnail sheet of the file (`VideoFileMetadataDto.Thumbnails`), served like every picture by the anonymous `/api/metadata-pictures/{id}` endpoint.
- Download streams the original file (`/api/public/shares/{shareId}/files/{indexedFileId}/download`), with no transcoding.

`CloseWhenConsumed` is only allowed on a single playable item. `SharePage` reports the player position to `POST .../progress`. At 90% of the runtime, or on a download, the share is marked consumed. The position comes from the visitor browser, so this is a convenience and not a guarantee: expiration and the access limit are the real barriers.

## Client

| Piece | Path |
|---|---|
| Landing and in-share page | `Pages/Shares/SharePage.razor` (`/share`, `/share/{ShareId}/m/{MediaId}`) |
| Layout (top bar reduced to the K7 symbol, regular players) | `Layout/ShareLayout.razor` |
| Media view (same hero as the media pages) | `Components/Shares/ShareMediaView` |
| Create / edit dialog, link dialog | `Components/Dialogs/ShareMediaDialog`, `ShareLinkDialog` |
| Menu entry | `Components/ShareMenuSection` (card menu, track menu, detail pages) |
| Management | `Components/MediaSharesManager`, used by `/admin/shares` and `/my-space/shares` |

`IPublicShareService` uses its own `HttpClient` without the sign-in redirect handler: a refused call must not send a visitor to the sign-in page. `IMediaSharingAccess` caches whether to offer **Share** to the current user (server switch plus capability, overrides included).

The share pages are marked `noindex` and `no-referrer` in the app shell.

## Tests

- `Domain.UnitTests/Entities/MediaShareTests`: status and session rules.
- `Application.UnitTests`: scope resolver, access guard for visitors, opening a share (password lock included), editing a share, session validation, media description, transcode output path checks.
- `Web.SmokeTests/PublicShareEndpointsTests`: end to end over HTTP. A visitor reads the share, gets 404 outside of it, is refused by the regular API and by streams outside the share. It also lists every `StreamAccess` route without a file id: a new one must be reviewed there. `CurrentUserTests` covers a real account combined with a share cookie.
- `Clients.ComponentTests`: `ShareMenuSection`, `ShareMediaView`.

## Cleanup

`MediaShareCleanupService` drops visitor sessions 30 days after they expire, with their technical devices. Shares themselves are kept until deleted, and are removed with their root media or their creator.
