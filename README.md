<p align="center">
  <img src="branding/logo.svg" alt="K7" width="160">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="License: AGPL-3.0"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10"></a>
</p>

K7 is a self-hosted media server for a small circle of family and friends.

The name comes from the French pronunciation `/ka.sɛt/` *(cassette)*. The idea is to bring back that feeling of a shelf at home: audio tapes, VHS, everything yours and local.

![K7 on TV, desktop, and mobile](screenshots/movie-showcase-devices.png)
More screenshots: [`screenshots/README.md`](screenshots/README.md).

## Demo

Access the live demo [here](https://k7.kaybi.dev). Only guest login is available, so features that require a full account are not accessible.

## Quick start (Docker)

```bash
cp .env.example .env   # set POSTGRES_PASSWORD
docker compose up -d   # pulls ghcr.io/kaybi-gh/k7:latest
```

Open `http://localhost:8080` and complete first-run setup. Install details: [docs/admin/install.md](docs/admin/install.md).

## Features

**Core**
- **Open source (AGPL-3.0)** - your server, your rules, no vendor lock-in
- **Simple to deploy** - no complex stack required
- **Movies, TV series, and music** - one shelf for everything you already own locally (more types may come)
- **Local auth + optional 2FA + optional OIDC** - works offline at home, plug in your identity provider when you need SSO
- **Guest mode** - optional limited access without a personal account (admin-controlled)
- **Transcoding (ffmpeg, HLS)** - play anywhere, even when the client or network cannot handle the original file
- **Federation** - link two K7 instances (yours and a friend's) to share and stream remote media and metadata without duplicating files

**Clients**
- **Web, Android (phone + TV), Windows, iOS, Mac** - same UI everywhere (Blazor WASM or MAUI + Blazor)
- **Fully keyboard-navigable** - spatial navigation across the whole app
- **Remote control** - drive playback on one device (TV, phone, browser) from another logged-in client on the same server
- **Chromecast (Web and Android only)** - cast video and music to any supported device
- iOS and Mac builds exist but are not tested by the maintainer (no Apple hardware)

**Personalization**
- **Server-wide defaults** - the admin can set default values for almost all customization (home layout, video player, playback, track selection, and more); each user can override them in their own settings
- **Custom home page** - pin what matters to you instead of a one-size-fits-all dashboard
- **Global media filters** - hide content you never want to see, across the whole app
- **Playback preferences** - quality, subtitles, intro skip, seekbar thumbnails, and more, so each viewing session feels right
- **Library and profile restrictions** - fine-tune who can access what beyond simple sharing rules
- **Editable metadata with field locks** - fix or override titles, descriptions and more, lock individual fields so metadata refreshes do not overwrite your changes

**Your space**
- **Playlists, collections, history, stats, reviews** - one place for "your" corner of the shelf, not buried in admin screens
- **Smart playlists** - rule-based lists (genre, year, play count, artist, and more) that re-evaluate as your library grows, like a mixtape that updates itself
- **Offline downloads** - take media on native clients and sync progress when you are back online

**Music discovery (AudioMuse AI)**
- Optional integration with self-hosted [AudioMuse AI](https://github.com/NeptuneHub/AudioMuse-AI) - mood radios, similar tracks, sonic paths, and playlists built from a text prompt, plus natural-language search in the library
- Keeps AI-assisted discovery on infrastructure you control; disabled by default until you connect your AudioMuse server

**Social**
- **Sync Play** - watch or listen together remotely, as if you were on the same couch
- **Shared profiles** - follow a series as a couple (or any group) with merged stats and history on each account
- **Visibility controls** - separate what you **share** and what you **accept to see**, per content type (playback history, reviews, collections, playlists, smart playlists) from and to whoever you want: nobody, your local server, federated servers, specific people

**Administration**
- **Dashboard, diagnostics, background tasks** - see what the server is doing without SSH
- **Outgoing notifications** - customizable webhook rules (event filters, payload templates) to plug K7 into your own tools
- **Import tool** - migrate from Plex, Jellyfin, Spotify, and more ([tools/K7.Import](tools/K7.Import/README.md))

## Documentation

All guides (users, admins, developers): **[docs/README.md](docs/README.md)**.

## Community

This project was built for my own specific needs. While I think it could benefit some people, making it free and open source does not mean I will work on every feature request.

Contributions are welcome:
- **Issues** - bugs (not security reports; see [SECURITY.md](SECURITY.md))
- **Discussions** - ideas, feature requests, and questions
- **Pull requests** - opening a PR does not guarantee a merge

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and to start contributing.

## Code of conduct

Be respectful and constructive. Harassment and bad-faith behavior are not tolerated. Maintainers may close or remove contributions that violate this.

## Support

While a simple thank you is more than enough, I do accept donations. If K7 is useful to you and you feel like chipping in, the **Sponsor** button on this repository is the simplest way to do that.

K7 also relies on many other open source projects and communities listed below, many of them welcome donations too.

## Special thanks

K7 would not exist without the work of many projects and communities, including:

- [FFmpeg](https://ffmpeg.org) - transcoding and media processing
- [OpenIddict](https://github.com/openiddict/openiddict-core) - authentication and OIDC
- [AudioMuse AI](https://github.com/NeptuneHub/AudioMuse-AI) - optional music intelligence
- [MusicBrainz](https://musicbrainz.org) - music metadata
- [TMDb](https://www.themoviedb.org) - movie metadata
- [TheTVDB](https://www.thetvdb.com) - TV series metadata
- [Wikimedia Commons](https://commons.wikimedia.org), [Wikidata](https://www.wikidata.org), and [Wikipedia](https://www.wikipedia.org) - artist images and person metadata
- [Chromaprint / AcoustID](https://acoustid.org/chromaprint) - audio fingerprinting
- [TagLibSharp](https://github.com/mono/taglib-sharp) - reading and writing media tags

See the in-app About page for a fuller list of credits.

## License

K7 is licensed under the [GNU Affero General Public License v3.0](LICENSE) (AGPL-3.0). If you modify K7 and provide it over a network, you must share your changes under the same license.
