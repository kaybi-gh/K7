# Design

UI constraints for shared Blazor clients. Code layout: [architecture.md - UI guidelines](architecture.md#ui-guidelines-summary).

## Audience

Small private installs: the owner plus family/friends. Same people on desktop, phone, and TV (remote / 10-foot). No cloud SaaS assumptions.

## Look and feel

- Dark mode is the default (warm near-black canvas). Light mode is a first-class theme, not a harsh invert.
- Surface chrome uses `--color-text` and `--color-accent-text` (a darker copper that meets WCAG AA on `--color-bg`). Copy over artwork uses `--color-text-on-media` on dark scrims. In light theme media heroes (desktop, mobile, and TV) use beige `--color-bg` scrims at the same opacities as dark, so logo, meta, and overview use `--color-text` like section titles.
- Media artwork (posters, covers, stills) carries the UI. Chrome stays quiet; do not decorate over the content.
- Ambient color (player backdrop, album hero blur) comes from the media when possible, not from a fixed brand palette.
- Accent is copper via `--color-accent` (`#d9b060` in both themes). Never hard-code palette hex in components. Hex in this doc is a token reference only.
- Fonts: Epilogue for headings, Manrope for body (`--font-heading` / `--font-body`). Do not use Inter/Roboto/Arial or monospace as a style gimmick.

## Loading

Page shells (home, explore feeds, movie, serie, season, episode, person, music artist, music album) use **skeletons** that match the layout. Desktop season loading uses the season skeleton (logo, icon actions, episode rows). TV season loading keeps the compact hero plus backdrop carousel. Person loading reuses the person hero shell (2:3 portrait, bio, filmography below the fold) so phone, desktop, and TV breakpoints match the loaded page. Library browse (`/library-groups/{id}`) shows a **CSS spinner** until the first page is ready, then MediaCards. Poster decode on those cards uses the CSS spinner, not a skeleton. **Spinners** stay for inline waits: saving, search, player buffering, dialogs, and admin jobs.

Hero backdrops (`MediaPageBackdrop`, TV home hero, TV season stills) decode fully then fade in. They stay at Medium (1920 CSS px) on typical TV viewports so Movie/Serie reuse the image already shown on Home / Explore instead of decoding a 4K original on the UI thread. Season episode stills follow the same Medium budget so D-pad focus does not decode full-resolution JPEGs. Rapid D-pad waits about 200 ms after focus settles before fetching the next hero JPEG, and cancels the in-flight decode. Title and meta follow focus immediately. Person backdrops rotate on a timer. Filmography hover and D-pad do not swap them. The person hero uses the person portrait, or a billed role portrait when the person has none. On TV Home and Explore, D-pad does not re-render the feed. Carousel rows mount all MediaCards once shown. TV vertical shelves grow a mounted range (never unmount a visited row) and only Blazor-render when a new shelf must appear. Explore row components stay alive so feeds prefetch before the shelf is shown. FeedHub keep-alive is unchanged. Delayed initial focus does not jump back to the first MediaCard.

Library browse episode stills use the same tile height as posters (wider 16:9 cells, not the same width). Stills never sit in a single full-width column: two is the minimum, and compact viewports cap at two (posters may still use three). Library browse on TV uses at most eight poster columns (larger tiles than a 10-column 1080p row) and Small posters (200 px) in the virtual grid. Virtualize overscan is four rows. Image warmup is the mounted window plus a short lookahead, not whole cached pages. First paint is a CSS spinner until the first page is ready, then MediaCards. Empty tiles only show when the network lags behind the mounted window. D-pad prefetches a couple of pages ahead of focus (mouse-wheel already advances Virtualize, which fetches that window). Fast scroll does not cancel in-flight pages. Jumping A to Z cancels out-of-range fetches and only fetches that range. When a page arrives, empty tiles become MediaCards in place. D-pad walks the catalog by row index so unloaded tiles can take focus. It only leaves the grid for the toolbar / navbar when the list is already at the top. D-pad focus does not re-render the page or steal scroll from the grid.

Library filter and sort overlays stay hidden until JS has written their position. C# owns `--placed` and `--teleported` on `K7Select` / `K7Menu` so a later render cannot wipe those classes and hide an open panel. They are not re-anchored on later renders (submenu, filter apply). On TV they open under the toolbar control. Focus inside menus and dialogs does not scroll the browse page. Media card overflow menus use a fixed width (title ellipsizes) so carousel cards do not open panels of different sizes. If the menu would clip the top of the page (first browse row), it opens below the trigger instead.

Component catalog: [developing.md - DesignSystem](developing.md#designsystem). Brand SVGs: [`branding/`](../../branding/).

## Theme tokens

Themes are a product surface. A later branding pass can ship a custom `[data-theme]` sheet or inject CSS through `K7.applyCustomCss`. Until then, hex lives only in `src/Clients/Shared/UI/wwwroot/css/themes/`. Override the names below, not component CSS.

**Surfaces:** `--color-bg`, `--color-surface`, `--color-surface-2`, `--color-drawer`, `--color-overlay`

**Brand:** `--color-primary` / `--color-primary-hover` / `--color-on-primary` are the shared brand gold (`#d9b060` / `#e8c878`) in every theme. `--color-accent` and `--color-accent-hover` match that gold. `--color-accent-text` is the darker copper for labels on light surfaces.

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

## Avoid

- Glassmorphism, gradient text, identical card grids, nested cards, hero-metric dashboards
- Bounce/elastic easing; gray text on colored backgrounds; pure `#000` / `#fff`
- Using `--color-accent` for labels on light surfaces (too light). Use `--color-accent-text`. Filled rating stars keep `--color-accent` so they stay gold in both themes.
- Falling back to `#fff` when a text token is missing
- Hiding primary actions behind mobile-only overflow menus when desktop shows them plainly
