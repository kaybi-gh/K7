# Design

UI constraints for shared Blazor clients. Code layout: [architecture.md - UI guidelines](architecture.md#ui-guidelines-summary).

## Audience

Small private installs: the owner plus family/friends. Same people on desktop, phone, and TV (remote / 10-foot). No cloud SaaS assumptions.

## Look and feel

- Dark mode is the default (deep blue-gray canvas, roughly `#0c1018`-`#131821`). Light mode must be equally usable, not a harsh invert.
- Media artwork (posters, covers, stills) carries the UI. Chrome stays quiet; do not decorate over the content.
- Ambient color (player backdrop, album hero blur) comes from the media when possible, not from a fixed brand palette.
- Accent is copper (`#CC7A3E`) via CSS tokens only - never hard-coded hex in components. Hex values in this doc are token references only.
- Fonts: Epilogue for headings, Manrope for body (`--font-heading` / `--font-body`). Do not use Inter/Roboto/Arial or monospace as a style gimmick.

## Loading

Page shells (home, explore feeds, movie, serie, season, episode, person, music artist, music album) use **skeletons** that match the layout. **Spinners** stay for inline waits: saving, search, player buffering, dialogs, and admin jobs.

Hero backdrops (`MediaPageBackdrop`, TV home hero, TV season stills) decode fully then fade in. They stay at Medium (1920 CSS px) on typical TV viewports so Movie/Serie reuse the image already shown on Home / Explore instead of decoding a 4K original on the UI thread. Season episode stills follow the same Medium budget so D-pad focus does not decode full-resolution JPEGs. Person backdrops rotate on a timer. Filmography hover and D-pad do not swap them.

Component catalog: [developing.md - DesignSystem](developing.md#designsystem). Brand SVGs: [`branding/`](../../branding/).

## Rules

1. Prefer artwork and playback over chrome. Drop UI that does not help find or play media.
2. No neon, glow, decorative gradients, or cyan/purple "AI" accents.
3. Desktop, mobile, and couch each need a real interaction model - do not only scale down the desktop layout.
4. Colors, spacing, and type that matter go through design tokens (themes are a product feature).
5. WCAG AA minimum: contrast, keyboard/spatial focus, semantic controls. Keep core features available on small screens.

## Avoid

- Glassmorphism, gradient text, identical card grids, nested cards, hero-metric dashboards
- Bounce/elastic easing; gray text on colored backgrounds; pure `#000` / `#fff`
- Hiding primary actions behind mobile-only overflow menus when desktop shows them plainly
