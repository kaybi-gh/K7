# Design

UI constraints for shared Blazor clients. Code layout: [architecture.md - UI guidelines](architecture.md#ui-guidelines-summary).

## Audience

Small private installs: the owner plus family/friends. Same people on desktop, phone, and TV (remote / 10-foot). No cloud SaaS assumptions.

## Look and feel

- Dark mode is the default (warm near-black canvas). Light mode is a first-class theme, not a harsh invert.
- Surface chrome uses `--color-text` and `--color-accent-text` (a darker copper that meets WCAG AA on `--color-bg`). Copy over artwork uses `--color-text-on-media` on dark scrims. In light theme media heroes (desktop, mobile, and TV) use beige `--color-bg` scrims at the same opacities as dark, so logo, meta, and overview use `--color-text` like section titles.
- Media artwork (posters, covers, stills) carries the UI. Chrome stays quiet; do not decorate over the content.
- Ambient color (player backdrop, album hero blur) comes from the media when possible, not from a fixed brand palette.
- Accent is copper via `--color-accent` (`#d9b060` in both default themes; other themes and the user's accent override set their own). Never hard-code palette hex in components. Hex in this doc is a token reference only.
- Fonts: Epilogue for headings, Manrope for body (`--font-heading` / `--font-body`). Do not use Inter/Roboto/Arial or monospace as a style gimmick.

## Loading

Page shells (home, explore feeds, movie, serie, season, episode, person, music artist, music album) use **skeletons** that match the layout. The home skeleton takes the shape of the home layout (`IHomeFeedStore.SkeletonRows`: the layout being loaded, else the previous one, kept on the device under `HomeSkeletonShape` with the id of its user so it is there when the app opens): a banner row holds the place of its banner and a landscape row shows wide placeholders. A `MediaCard` skeleton follows the card style of the user (hidden title or subtitle, details line, hover effect). Desktop season loading uses the season skeleton (logo, icon actions, episode rows). TV season loading keeps the compact hero plus backdrop carousel. Person loading reuses the person hero shell (2:3 portrait, bio, filmography below the fold) so phone, desktop, and TV breakpoints match the loaded page. Library browse (`/library-groups/{id}`) shows a **CSS spinner** until the first page is ready, then MediaCards. Poster decode on those cards uses the CSS spinner, not a skeleton. **Spinners** stay for inline waits: saving, search, player buffering, dialogs, and admin jobs.

TV (`html.platform-tv`) never uses `backdrop-filter`: a global rule in `app.css` removes it, so blurred surfaces fall back to their tinted background. Media cards only promote the title marquee to a compositor layer while it scrolls.

Hero backdrops (`MediaPageBackdrop`, TV home hero, TV season stills) decode fully then fade in. They stay at Medium (1920 CSS px) on typical TV viewports so Movie/Serie reuse the image already shown on Home / Explore instead of decoding a 4K original on the UI thread. Season episode stills follow the same Medium budget so D-pad focus does not decode full-resolution JPEGs. Rapid D-pad waits about 200 ms after focus settles before fetching the next hero JPEG, and cancels the in-flight decode. Title and meta follow focus immediately. Person backdrops rotate on a timer. Filmography hover and D-pad do not swap them. The person hero uses the person portrait, or a billed role portrait when the person has none. On TV Home and Explore, D-pad does not re-render the feed. The TV season page applies episode focus (hero, backdrop, cast) without a page render either. Carousel rows mount all MediaCards once shown. TV vertical shelves grow a mounted range (never unmount a visited row) and only Blazor-render when a new shelf must appear. Explore row components stay alive so feeds prefetch before the shelf is shown. FeedHub keep-alive is unchanged. Delayed initial focus does not jump back to the first MediaCard.

Library browse episode stills use the same tile height as posters (wider 16:9 cells, not the same width). Stills never sit in a single full-width column: two is the minimum, and compact viewports cap at two (posters may still use three). Library browse on TV uses at most eight poster columns (larger tiles than a 10-column 1080p row) and Small posters (200 px) in the virtual grid. Virtualize overscan is four rows. Image warmup is the mounted window plus a short lookahead, not whole cached pages. First paint is a CSS spinner until the first page is ready, then MediaCards. Empty tiles only show when the network lags behind the mounted window. D-pad prefetches a couple of pages ahead of focus (mouse-wheel already advances Virtualize, which fetches that window). Fast scroll does not cancel in-flight pages. Jumping A to Z cancels out-of-range fetches and only fetches that range. When a page arrives, empty tiles become MediaCards in place. D-pad walks the catalog by row index so unloaded tiles can take focus. It only leaves the grid for the toolbar / navbar when the list is already at the top. D-pad focus does not re-render the page or steal scroll from the grid.

Library filter and sort overlays stay hidden until JS has written their position. C# owns `--placed` and `--teleported` on `K7Select` / `K7Menu` so a later render cannot wipe those classes and hide an open panel. They are not re-anchored on later renders (submenu, filter apply). On TV they open under the toolbar control. Focus inside menus and dialogs does not scroll the browse page. Media card overflow menus use a fixed width (title ellipsizes) so carousel cards do not open panels of different sizes. If the menu would clip the top of the page (first browse row), it opens below the trigger instead.

Component catalog: [developing.md - DesignSystem](developing.md#designsystem). Brand SVGs: [`branding/`](../../branding/).

## Theme tokens

Themes are a product surface, and they are data, never CSS. A theme is nine colors on a dark or light base (`ThemePaletteDto` in `K7.Shared/Theming`): background, surface, secondary surface, text, secondary text, muted text, accent, text on accent, accent for text. `ThemeCssBuilder` derives every other color token from those and writes them on the root element over the base stylesheet (`K7.applyAppearance`). Only validated `#rrggbb` values and fixed strings are written, so a shared theme file cannot inject CSS.

- **Base themes** (`default-dark`, `default-light`) are the two stylesheets in `src/Clients/Shared/UI/wwwroot/css/themes/`. `data-theme` on `<html>` is always one of the two, so `[data-theme="default-light"]` rules cover every light theme. Hex lives only there and in `BuiltInThemes`.
- **Built-in themes** (`BuiltInThemes`) and **custom themes** (made in the editor or imported as a `k7-theme` JSON file, stored with the user's appearance preferences) layer their palette over a base.
- **Server themes** (`ServerThemes`, server setting `ServerThemes`) are custom themes made by an administrator under Admin -> Experience -> Interface -> Appearance. Same palette and file format, ids prefixed `server-`. They travel in `ServerInfoDto.ServerThemes`, show in every account's theme picker, and can be the server default theme. Deleting the default one puts the default back on `default-dark`.
- Every palette must pass `ThemePaletteValidator` (WCAG AA for text and accent, 3:1 for muted text). Built-in themes are covered by a test; the editor and the import refuse a palette that fails.
- **Appearance preferences** (`AppearancePreferencesDto`, user setting `Appearance`): theme, optional accent override, text size, corner style, plus an optional set per device type (desktop, phone, tablet, TV). They follow the account. The last look is cached in `localStorage` so `theme.js` restores it before Blazor starts. The preferences themselves are also copied to the device with the id of their user (`AppearanceCache`, key `AppearancePreferencesCache`, with the card style of the server): `MainLayout` applies the copy of the last user at start, so cards open in the right style and the home asks for the right feed before the server answers. The server answer replaces the copy.
- The mini music player has two slots, in `AppNav` and in the bottom dock of `MainLayout`; each copy shows when its media query matches (`MiniPlayerSlots`). By default the bar takes wide screens and the dock takes phones. The appearance setting `MiniPlayer` (`MiniPlayerPlacement`, per device type, set up on the music player page and part of the music player settings: it follows the server defaults with them, see the skins below) can give every width to the dock: the bar slot then stays empty (it still pushes search and the profile to the right), `app-shell--player-bottom` is set on the shell, and on wide screens the dock copy renders as a playback bar (`MiniMusicPlayer` parameter `Bar`): edge to edge, no radius, no gap, a small play button, plus shuffle, previous and repeat while the bar is wider than 768 px. The page area leaves the bar free (`.app-main` bottom padding while something plays) instead of running under the player, so scrolling pages and the fixed TV pages need nothing of their own.
- **Skins** of media pages and players are appearance settings too (`AppearanceSettingsDto`: `MediaPageHero` (faded, clean cut, banner), `MediaPageHeroSize` (full page is the immersive page, half, third), `SeasonPage` (`SeasonPageLayout`: the episodes of a season as a list or a grid; a TV always shows the immersive page with its carousel, and is the only device to; `SkinClasses.SeasonLayout` resolves it for a device and `SeasonLayouts` lists what a device can show), `MediaPagePoster`, `MediaPageOverrides` (a kind of page, `MediaPageKind`, with a `MediaPageSkinDto` of its own; `PageSkin(kind)` resolves it and `MediaPageRoutes.KindOf` tells the kind of the page on screen), `MediaPageLists`, `MediaPageGridMore`, `Audio*`, `Video*`, per device type like the rest; pages Settings -> Interface -> Media pages / Music player / Video player are `AppearanceSettingsPanel` with another `Section`). The server has defaults for them (`ServerInfoDto.DefaultSkins`, stored by `PUT /api/admin/settings/skins`, edited with the same panel in `ServerDefaults` mode): each part (media pages, music player, video player) follows them as a whole until the user sets it up (`MediaPagesOwn` and its two siblings, or any value that is not the built-in one), and `ThemeService.Settings` is the merged result (`WithServerSkins`). The settings panel only offers what the device being set up can show. A skin is never a second copy of a component: it is a class on the same markup, computed by `SkinClasses` (`media-pages--hero-cut|banner`, `media-pages--size-half|third` and `media-pages--poster` on the app shell, which combine freely; the banner turns `.media-hero-main` into a rounded card that is a window onto the backdrop, which stays behind the page: a frame of the page color drawn as its shadow hides the rest, `fullscreen-player--*` on the music player, `video-controls--*` on the video controls), and the default adds no class. Rules for a new one: style it in the "Skins" block at the end of the stylesheet of the component, keep phones and TV working (media page variants are styled twice in `media-hero.css`: from 600 px for wide screens and TV, and under it for the stacked page of phones; on TV a grid is walked in reading order by `handleCarouselNav`, which skips the cards a limited grid hides, the carousels kept in the hero zones stay carousels, and `K7.TvDetailScroll.belowOffset` is where its "below" zone starts whatever the height and margins of the hero), and add the class to `SkinClassesTests`. Sections of a media page are wrapped in `MediaSections`; a `Carousel` inside it shows as a grid when the lists setting says so (its `Grid` parameter, Embla is then not started; `GridRows` limits it to its first lines, `carousel.js` `limitGrid` hides the cards after them and shows the "show more" button); the optional poster is `MediaHeroPoster`; a `PersonCard` takes the hover effect of the common card style, like a `MediaCard`; the waveform ring is `WaveformRing`. `SkinPreview` is the sketch shown with those settings: give it the new option too. The navigation settings have theirs, `CustomNavPreview` (in `CustomNavLayoutFields`, so the personal page and the server default both show it): every page of the interface settings draws what it changes. It takes the device the settings are for (`Device`): a phone is drawn upright with the stacked page phones keep, and options that do not reach a device (wide-screen variants on phones, everything on TV) are not drawn. The native player (`NativeVideoPlayerOverlay.ApplySkin`) follows the video layout, the middle controls and the progress color.
- Text size scales the root font size (type tokens are in `rem`) through the `--k7-text-scale` factor: it multiplies the base size of the platform (larger on TV) and never replaces it. A fixed `px` size next to `rem` text (cover, button) stops matching it on TV: size both in `rem`. On Android TV the font size of the system is a second factor (`--k7-system-text-scale`): the host turns the text zoom of the WebView off, because it enlarges text and not the boxes around it, and sends the factor in the user agent (`TelevisionLayout.TextScaleMarker`, read by `tv-layout.js`). Corner style overrides the `--radius-*` scale. Keep new sizes in `rem` and radii on the tokens so both keep working.
- The rules below (copper accent, no pure `#000` / `#fff`) describe the two default themes. Other themes may use their own accent, and the Black theme uses a true black background for OLED screens.

Override the names below, not component CSS.

**Surfaces:** `--color-bg`, `--color-surface`, `--color-surface-2`, `--color-drawer`, `--color-overlay`

**Brand:** `--color-primary` / `--color-primary-hover` / `--color-on-primary` are the shared brand gold (`#d9b060` / `#e8c878`) in both default themes, and follow the accent elsewhere. `--color-accent` and `--color-accent-hover` match that gold. `--color-accent-text` is the darker copper for labels on light surfaces.

**Text:** `--color-text`, `--color-text-secondary`, `--color-text-muted`, `--color-text-on-media`, `--color-text-on-media-muted`, `--color-text-on-media-accent`

**Feedback:** `--color-success`, `--color-error`, `--color-warning`, `--color-info` are for text and icons. Filled chips and buttons use `--color-success-fill`, `--color-error-fill`, `--color-warning-fill` with `--color-on-fill` (same dark ink as `--color-on-primary`).

**Media heroes:** `--media-scrim` (black in dark, `--color-bg` in light) and `--media-copy-shadow` (tight scrim shadow in dark, `none` in light). `--media-watched-chip` is the watched badge on posters (dark translucent overlay in dark, translucent `--color-surface` in light). Artwork overlays such as poster titles and video chrome may keep a real black shadow. `#000` masks stay as masks.

**Elevation:** `--shadow-color` plus `--shadow-sm` through `--shadow-xl` (`color-mix` of `--shadow-color`)

Type, space, and radius stay in `tokens.css` (Open Props-style scales, not the package).

## Rules

1. Prefer artwork and playback over chrome. Drop UI that does not help find or play media.
2. No neon, glow, decorative gradients, or cyan/purple "AI" accents.
3. Desktop, mobile, and couch each need a real interaction model - do not only scale down the desktop layout.
4. Colors, spacing, and type that matter go through design tokens (themes are a product feature).
5. WCAG AA minimum: contrast, keyboard/spatial focus, semantic controls. Keep core features available on small screens.

## Home rows

A home row has a display type (`HomeRowDisplayType`, saved in the layout: only add values at the end), in two
families the row dialog asks for in two steps: a carousel (`Carousel` for posters and covers, the default, or
`Landscape` for 16:9 cards) or a banner (`Featured` contained, `FeaturedBleed` edge to edge; `IsBanner()`).
`HomeRowDisplay` picks the items and the card variant for both `HomeView` and `HomeLayoutPreview`, so the
preview never drifts from the page. `HomeLayoutPreview` is live: it re-reads the rows on every render of the settings
page, and only fetches a row again when what it lists changed (scope, sort, size), so reordering, hiding or
restyling is instant. It is a reduced picture (`zoom: 0.6`) and `inert`: editing happens in the list.

- Landscape cards use `MediaCardViewModel.LandscapeUrl` (backdrop, or still for an episode, at card size).
  Every card of the row stays 16:9: an item without landscape art shows its poster or cover, cropped.
- A banner row renders `HomeHero` with the first items that have a backdrop (no music), as many as the page
  size of the row asks for (3 to 15 in the dialog, 15 at most). The logo of a media (`LogoUrl`) replaces its
  title, which stays as the text alternative and comes back if the logo fails to load. The TV home hero
  (`HomeTvHero`) does the same. It is a row
  like the others: its place in the list, its libraries and its sort are the user's choice. Switching titles
  is manual (dots) unless the row sets `AutoAdvanceSeconds` (off by default). Auto-advance pauses on hover and
  on keyboard focus (a key released inside the banner; a click that leaves the focus behind does not hold it), shows a pause button, and does not run under `prefers-reduced-motion` or on a hidden page
  (`js/home-hero.js`, a module loaded on the first tick). While it waits, the active dot is a track that
  fills for the same duration and pauses with the same state (`home-hero--holding`). The banner itself is an arrow-key target (`focusable`,
  `tabindex="-1"`) that relays the focus to its button: the button sits in a corner, and spatial navigation
  only looks straight up or down, so the banner was skipped from the row below. A change of title
  cross-fades: the previous artwork fades out underneath and is gone, so a partly transparent banner never
  shows two pictures. It uses the same scrim tokens as the TV hero. On TV,
  or with nothing to feature, the row shows as posters. Banner rows ask for the detailed feed (overview).
  The bleed style drops the margins and the radius, lets the artwork and its scrim run past the banner, behind the
  top of the next row (rows are stacked each above the one before, `.home-row`; the banner keeps its own place so its buttons stay clickable), with a long eased mask that fades to
  nothing (the page shows through: painting `--color-bg` there left a visible edge), and raises its copy above the stretch that fades out (same on-media text tokens as the contained style). When it
  is the first thing on the page it cancels the top padding of the page. With the shortcuts row of the custom navigation above it,
  it still starts at the very top: the row leaves the flow and sits on the artwork, over a scrim of the page color (`HomeView.razor.css`).
- A home has no banner or one banner, always first (`HomeLayoutRules`): `HomeRowList` snaps it back to the top
  and the row dialog does not offer the banner when another row is one, both validators refuse anything else,
  and `HomeLayoutSanitizer` fixes stored layouts when they are read.
- `HomeRowList` edits the rows (order, visibility, add, edit, delete) for a personal home and for the server
  default. Each row shows two summary lines built by `HomeRowSummary` with the labels of the dialog. The banner row is locked
  (`K7SortableList.IsLocked`: a pin instead of a handle). Adding a row offers `HomeRowPresets`: the smart rows
  of the default home, whose titles are translated keys and whose sorts the dialog does not list.

## Media cards

Card style is a user preference (`MediaCardPreferencesDto` in `AppearancePreferencesDto.Cards`, null to follow
the server) with a server default (server setting `DefaultCardStyle`, sent in `ServerInfoDto.DefaultCards`,
edited under Admin -> Experience -> Interface -> Media cards). `ThemeService.EffectiveCards` is the user's style,
else the server default. `MediaCardStylePanel` edits both. A style is a common
`MediaCardStyleDto` plus optional styles for movies, series and music (`CardMediaGroup`). A style says whether
the title and subtitle show, which details show (`CardDetailField`), where they go (`CardDetailPlacement`:
line under the card, badges on the artwork, badges on hover) and the hover effect (`CardHoverStyle`).

- `MainLayout` cascades a `MediaCardStyleContext` around the page area. `MediaCard` takes it as a cascading
  parameter and resolves its style from the kind of media, so no page passes anything. No context (default
  preferences, tests, cards outside the layout) means the default card. TV applies it too: its shelves
  measure their rows, so taller or shorter cards fit; only the virtual library grid has a row height in code,
  which adds the details line when the style has one.
- A new context instance is cascaded only when the card preferences changed, and it is part of
  `MediaCard.RenderState`: a theme change does not re-render every card.
- Details come from `MediaCardDetails`, in a fixed order, keeping only what the item has. Overview, runtime,
  rating and genres only travel with the detailed home feed: `HomeView` asks for it through
  `IHomeFeedStore.SetDetailedAsync` when `NeedsDetailedFeed` is true, and Explore rows (`HomeFeedCarouselRow`) do
  the same, so accounts with default cards keep the light feed. Library grids ask for them the same way (`QueryMediasRequest.IncludeCardDetails`, filled by
  `LiteMediaCardDetailsService` with three queries per page, cached with the page). Without the flag the
  browse query and its cache key are unchanged, and TV never sets it (its grid shows the year only). A details line under the card adds
  18px to the fixed footer height of the grid.
- The hover effect applies to the pointer and to keyboard focus, and zoom and lift replace the accent outline
  in both cases: the effect is then the focus indicator. A selected card and a card with its menu open keep the
  outline, and so does focus under `prefers-reduced-motion`, where there is no movement to show it. Badges on hover stay visible on touch screens. Both respect `prefers-reduced-motion`.
- Badges use `--media-watched-chip` and `--color-text-on-media`, like the watched badge.

## Settings pages

Long settings pages must not turn into one tall scroll (painful with a remote) nor stretch
controls across a wide screen:

- `K7SettingsSplit` + `K7SettingsNavItem`: an index (icon, title, short description of what the
  section is for) and one sub-page at a time. Side by side once the content area
  reaches about 52rem (window of 1100px with the page sidebar open, 900px with it folded). Below that and on
  TV it behaves like a menu: the index, then the sub-page as its own screen with a back link and
  the page header hidden. Each sub-page has its own route (`/settings/video-playback/{section}`).
  `Extra` (with `ExtraTitle`) adds a second panel under the sub-page for settings that sit there
  but are about something else, e.g. theme songs under the media page layout.
- Pages that stay separate components can still share one index: `InterfaceSettingsSplit` wraps
  the appearance (theme, accent, text size, corners, own themes, language), media cards, home layout, navigation, media pages and player pages (personal
  and admin) under `/settings/interface` and `/admin/interface`. The former addresses stay
  routable because saved shortcuts point at them. Reset to defaults is per sub-page.
  The index is the same on both sides, in the same order (`InterfaceSettingsSplitTests`): a
  setting that moves on the personal side moves on the admin side too. Theme songs have no
  entry: they are the `Extra` panel of the media pages, personal and admin (the former
  `/theme-songs` addresses open the media pages).
  `AdvancedSettingsSplit` does the same for the admin-only Advanced entry (transcoding, link
  previews) under `/admin/advanced`.
  A save bar that belongs to one sub-page goes on `settings-form-page--section-actions`, which
  hides it while the index is shown alone.
- Personal settings and their server defaults mirror each other: same entry name, same
  sub-pages, same keys (`/settings/video-playback/subtitles` and
  `/admin/video-playback/subtitles`). `SettingsMirrorLink` in the page header jumps to the
  counterpart on the same sub-page (pairs listed in `SettingsMirror`). The link towards the
  admin only shows to accounts with `CanAccessAdmin`. Register a new pair when adding a
  settings page that has server defaults.
- Admin lists of items (users, devices, restriction profiles, notification rules) use the same
  shell instead of a table: `K7EntityRow` lines as the index (visual, title, secondary line,
  state chips), the selected item's sheet as the detail, at its own address
  (`/admin/users/{id}`). The sheet shows fields as `K7SettingRow`s and what can be done as
  `K7SheetAction` lines whose badges say what is already set. Row menus and inline controls
  move to the sheet, so every action is reachable with a remote. Libraries and library groups
  follow the same shell: the scan of a library is an action of its sheet.
- Dialogs use the same rows as pages: a form dialog is a list of `K7SettingRow`s (label and
  hint on the left, control on the right, `Stacked` for multi-line text, editors and lists,
  `Required` to keep the asterisk the field's own label used to show). A dialog that picks
  users or items is a search field over a `K7CheckboxList` with avatar or icon, name and a
  secondary line. Viewers, help texts, picker grids and confirmations are not forms and stay
  as they are.
- Action menus (card context menus, "..." menus) share one structure: `K7MenuSection`s in a
  fixed order - play or open (one `Primary` entry at most) with the "go to" shortcuts right
  below, my state, organize, manage, destructive (`Danger`, always last). A label says what happens: "Open the page" on a
  series, not "Play". Sections draw their own separators, so no raw divider in a menu.
- `K7SettingRow`: one setting per line, label and hint on the left, compact control on the right.
  Sliders and text fields go in a `settings-field-group` between rows.
- `K7OptionList`: short single choices shown in full instead of a select.
- Saving follows one rule. Personal settings save on change, with no Save or Cancel: the page
  shows a `K7SaveIndicator` (spinner from the first pending edit to the end of the request, then
  a round check) beside **Reset to defaults**, in the split footer (`SaveState`) or in
  `K7SettingsActionBar` with `AutoSave`. `SettingsAutoSaveSession` holds the logic (debounce,
  state, flush when leaving the page). Server defaults in the admin keep the explicit Save and
  Cancel of `K7SettingsActionBar`, since they reach every account. A lone admin switch (link
  previews) saves on change too: a Save bar for one switch is more ceremony than safety. Forms that need a deliberate
  confirmation (password, PIN, account creation) keep their own button.
- Rows shared by a personal page and its admin defaults live in one fields component that takes
  the section key (`VideoPlayerSettingsFields`, `TrackSelectionSettingsFields`,
  `AudioPlayerExperienceSettingsFields`).

Video and audio playback (settings and admin) are the reference implementations.

## Avoid

- Glassmorphism, gradient text, identical card grids, nested cards, hero-metric dashboards
- Bounce/elastic easing; gray text on colored backgrounds; pure `#000` / `#fff`
- Using `--color-accent` for labels on light surfaces (too light). Use `--color-accent-text`. Filled rating stars keep `--color-accent` so they stay gold in both themes.
- Falling back to `#fff` when a text token is missing
- Hiding primary actions behind mobile-only overflow menus when desktop shows them plainly
