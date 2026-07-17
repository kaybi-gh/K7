# Using K7

Guide for people using a K7 server that someone else (or you) already installed. Administrators: see [Install & run](../admin/install.md).

## Getting started

### Get an account

How you sign in depends on how the administrator configured the server:

| Mode | What you do |
|---|---|
| Admin-created local account | Email and password on the sign-in page |
| Self-registration | Sign-up page when the admin enabled it (**disabled by default**) |
| OIDC / SSO | Provider button or automatic redirect when SSO is enabled (**disabled by default**) |
| Guest | Welcome screen -> continue as guest when Guest is active (Admin -> Users; **inactive until enabled**) |

Guest mode is limited: many Settings sections and personal features (My Space, offline, and similar) are hidden, and playback progress is not kept like a full account.

### Two-factor authentication (2FA)

For password accounts, under **Settings -> Account**: enable 2FA, scan the QR code (or enter the shared key), confirm with a code, and store the recovery codes. You can regenerate codes or disable 2FA on the same page. At sign-in you enter an authenticator code (optional "remember this machine").

### After sign-in

1. Choose a profile on the **profile selection** screen if prompted (your user and any shared profiles pinned on this device).
2. Browse from Home, or open **My Space** for playlists, history, and more.
3. Tune playback and privacy under **Settings**.

## Clients

Same interface in the browser and in native apps.

| Platform | Notes |
|---|---|
| Web | Open the server URL in a browser - no separate install |
| Android (phone) | Native app |
| Android TV | Native app for TV remotes |
| Windows | Native app |
| iOS / Mac | Native apps exist but are **not tested** (no Apple hardware) |

Native builds: GitHub Releases when published, or ask your admin. On first launch, enter the server address (for example `k7.example.com`). **https** is assumed; use `http://...` only for plain HTTP on a local network. The app checks that the server responds, then saves the address.

After that first setup, the app **closes** (or exits to the home screen). Open it again and sign in. This restart quirk is a **known limitation** of the native apps.

Change server later: **Settings -> General** -> disconnect (trash), then enter a new address (same close-and-reopen behavior may apply). The web app always uses the server that hosts the page (address shown read-only in General).

## Playback

### In the player

- **Quality** for this session (Original or a lower ladder) - not saved as a lasting preference
- **Audio** and **subtitle** tracks
- **Intro / outro skip** when markers exist and your settings allow it
- **Play on device** for remote control or Chromecast - see [Watching together and casting](#watching-together-and-casting)

### Settings

The administrator can set **server-wide defaults**; yours override them (reset available on those pages). Details for video, subtitles, tracks, and audio: see [Customization](#customization).

| Page | Highlights |
|---|---|
| Settings -> Video playback | Intro/outro skip, subtitle appearance, resume / continue-watching, seekbar thumbnails |
| Track selection | Preferred audio languages; when to show subtitles (Off, Forced only, Full, Hearing impaired) |
| Settings -> Audio player | Music preferences: loudness normalization, equalizer, crossfade, autoplay, streaming quality, player behavior, resume |

Seekbar thumbnails only appear if the server generated them.

### Touch (phone / tablet)

| Gesture | Action |
|---|---|
| Double-tap left half | Seek backward (~10 s; repeats accumulate) |
| Double-tap right half | Seek forward |
| Vertical drag left half | Brightness |
| Vertical drag right half | Volume |

Single tap shows or hides controls when you are not mid-gesture.

### Keyboard and TV (controls hidden)

| Input | Action |
|---|---|
| Left / Right | Seek (~10 s; repeats accumulate) |
| Up / Down | Volume |
| Enter / Select | Show controls, or skip intro when offered |

The rest of the app is spatially navigable (arrows / D-pad). Android TV also handles media Play/Pause/Stop and long-press Select where relevant.

## Customization

Almost everything personal can be tuned under **Settings**. The administrator may set **server-wide defaults**; your choices override them, and settings pages usually offer a reset to those defaults.

| Area | Where | What you can change |
|---|---|---|
| Profile | Settings -> Account | Avatar, display name (also password, email, PIN, 2FA for password accounts) |
| Look and language | Settings -> General | Theme (light / dark), interface language |
| Home | Settings -> Home | Which rows appear on Home and in which order (with preview) |
| Libraries | Settings -> Libraries | Hide libraries you do not want to browse (among those the admin already allows) |
| Hidden media | Settings -> Hidden | Review and unhide titles you previously hid |
| Video and subtitles | Settings -> Video playback / track selection | Intro skip, subtitle look, resume rules, preferred audio / subtitle languages - see [Playback](#playback) |
| Music player | Settings -> Audio player | Music preferences: loudness normalization, equalizer, crossfade, autoplay, streaming quality, player behavior, resume |
| Offline | Settings -> Offline | Storage and network rules on native apps - see [Offline downloads](#offline-downloads) |

Session **quality** stays in the player menu only (not a saved preference).

## My Space and libraries

Libraries are on Home (and library browse). What you see depends on admin library / profile access, plus your own library exclusions above.

**My Space** is your personal corner:

| Area | Notes |
|---|---|
| Playlists | Manual lists and **smart playlists** (rules that refresh as the library grows). With AudioMuse, you can also build a playlist from a text prompt - see [Music discovery](#music-discovery-audiomuse). |
| Collections | Group titles your way |
| Stats / History / Reviews | Your activity and ratings |
| Downloads | Native apps only - see [Offline](#offline-downloads) |

Playlist views can include a "show shared" option. What others see of your playlists, collections, reviews, and history is controlled under [Privacy](#privacy-and-visibility) (and per-item visibility where offered).

## Privacy and visibility

Sharing is **opt-in**. By default, social content scopes are **Nobody**: nothing is shared until **you** widen a scope. The administrator **cannot force you to share** (there is no server-wide "everyone must share history/reviews" switch). They can still limit which libraries or profiles you may access - that is access control, not forced social opt-in.

Configure this under **Settings -> Social**. K7 separates **what you share** from **what you want to see**, for reviews, collections, playlists, smart playlists, and playback history. You can also blur reviews until you have watched the media.

| Scope | Meaning |
|---|---|
| Nobody | Only yours (default) |
| Local server | Same K7 instance |
| Federation | Directly peered servers only (not friends-of-friends) |
| Specific people | People you pick |

Per-item visibility on playlists or collections can tighten or target sharing further when the UI offers it.

## Offline downloads

**Native apps only** (not the web app). Download from movies, episodes, albums, artists, or playlists; manage under **My Space -> Downloads**.

**Settings -> Offline**: storage limits, Wi-Fi vs mobile data, music cache lookahead. Progress made offline syncs when the server is reachable again. If the server does not answer on profile selection, you may be offered **Continue offline**.

## Watching together and casting

### Remote control

Control playback on another logged-in device on the **same server**: open **Play on device** in the player, pick the target, use transport controls (you can resume on the controller later). Needs a live connection to the server.

### Chromecast

**Web and Android only.** Use the Chromecast section in Play on device. The Cast device must be on the same network as usual.

### Sync Play

Synchronized session with chat, reactions, and shared play/pause/seek: create from the Sync Play dialog in navigation, invite or share the link. Guests can join with a nickname when Guest is active and the invite allows it. **Settings -> Sync Play** controls invitations on the device and account.

## Shared profiles

Shared profiles let a couple or group watch together with a **shared continue-watching / history / stats bucket** (**Settings -> Shared profiles**). Personal progress stays separate: group watches do not appear in a member's personal history, and activating a shared profile does not show another member's private watch data.

1. Create a profile (name, at least two members, a host, optional PIN).
2. Others cannot add you until you allow shared-profile invitations.
3. Pin with **Show on this device**, then pick it on profile selection.
4. The host can **Configure** playback policies, content restrictions, home layout, and playlists shared with members for navigation.

While a shared profile is active:

- Home continue-watching, playback history, and watch stats are scoped to the profile only. Personal history never mixes with group history in either direction.
- The effective home layout is the profile's own layout if the host set one, otherwise the server default (never a member's personal layout).
- Content restrictions come from the profile's assigned restriction profile, not the acting member's personal restrictions.
- Playlists shared to the profile appear in members' navigation for the duration of the session.
- Reviews stay personal. Leaving may transfer the host; if only one member would remain, the profile is removed.

## Music discovery (AudioMuse)

Optional. The admin connects a self-hosted [AudioMuse AI](https://github.com/NeptuneHub/AudioMuse-AI) under Admin -> Music intelligence (**disabled by default**). When off, AI features are hidden.

When on: similar / ambiance radios and sonic paths under **Music -> Radio**, intelligent search in the library, similar tracks in the music player, and AI smart playlists from a text prompt. Basic radios (random, time capsule, recently added) work without AudioMuse.

## When something goes wrong

| Problem | What to try |
|---|---|
| Playback will not start or buffers a lot | Lower quality in the player; check network; on native apps, confirm the saved server address; ask the admin if the file needs transcoding |
| No subtitles | Pick a track in the player; check track-selection settings; the file may have no subtitle streams |
| Progress missing | Guests do not keep progress like full users; offline sync needs a later connection; with a shared profile, confirm you used the right profile |
| 2FA code rejected | Check the phone's clock; use a recovery code |

Anything else: tell your admin roughly when it happened and which client you used.
