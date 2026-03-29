# UI Guidelines

This document covers the Blazor UI architecture, component conventions, and theming for K7. For backend conventions, see [`CodingConventions.md`](CodingConventions.md).

## Architecture

K7 uses Blazor WebAssembly (browser) and MAUI Blazor Hybrid (mobile/desktop) sharing the same UI layer:

| Project | Role |
|---|---|
| `Clients/Shared/K7.Clients.Shared.Components` | Reusable Blazor components |
| `Clients/Shared/K7.Clients.Shared.Pages` | Pages, layouts, `wwwroot/app.css` |
| `Clients/Shared/K7.Clients.Shared.Services` | Services, state, HTTP clients, resources |
| `Clients/Shared/K7.Clients.Shared.Models` | Client-side models and DTOs |
| `Clients/Web` | Blazor WASM host |
| `Clients/MAUI` | .NET MAUI Blazor Hybrid host |

New pages go in `Clients/Shared/Pages/`. New reusable components go in `Clients/Shared/Components/`. Platform-specific code stays in the respective host project.

## Component Structure

Use the component triad pattern — three files per component:

| File | Purpose |
|---|---|
| `MyComponent.razor` | Markup (Razor template) |
| `MyComponent.razor.cs` | Code-behind (partial class, `@inject` equivalents, lifecycle, event handlers) |
| `MyComponent.razor.css` | Scoped CSS (Blazor CSS isolation) |

Keep `.razor` files focused on markup. Logic goes in the `.razor.cs` code-behind.

## MudBlazor

MudBlazor is the current component library. Key conventions:

- Use MudBlazor components (`MudButton`, `MudTextField`, `MudDataGrid`, etc.) instead of raw HTML for interactive elements.
- Wrap MudBlazor components in K7-specific components when the same configuration is reused across multiple pages.
- `CustomMudProviders.razor` registers `MudPopoverProvider`, `MudDialogProvider`, and `MudSnackbarProvider`.
- Reference MudBlazor CSS variables in scoped styles (see below).

## Theming

Theming is centralized in two files:

- **`Services/Resources/Themes.cs`** — defines `ThemeWrapper` instances with `PaletteLight` and `PaletteDark` palettes. Currently ships "Plex" (default) and "MudBlazor default". Available via `Themes.Collection` (`FrozenSet<ThemeWrapper>`).
- **`Services/ThemeService.cs`** — holds current theme + dark mode toggle. Exposes `ThemeOnChange` and `DarkModeEnabledOnChange` events for layout re-rendering.

Rules:

- Never hardcode colors in components. Use MudBlazor's theme palette or CSS variables.
- New themes: add a `ThemeWrapper` to `Themes.cs` with both `PaletteLight` and `PaletteDark`.
- Default is dark mode enabled (`DarkModeEnabled = true`).

## Layout

Two layouts defined in `Clients/Shared/Pages/Layout/`:

| Layout | Used by |
|---|---|
| `MainLayout` | Default — sidebar (desktop) or mobile footer, media players, theme provider |
| `EmptyLayout` | Login / select-user pages — bare theme provider + body |

`MainLayout` uses `MudBreakpointProvider` for responsive behavior:
- `Breakpoint.Xs` → mobile layout with `MobileFooter`
- `Breakpoint.SmAndUp` → desktop layout with `Sidebar`

## CSS Conventions

### Scoped Styles

Prefer Blazor CSS isolation (`.razor.css`) over global styles.

### CSS Variables

Use MudBlazor's CSS variables for consistency with the active theme:

```css
/* Good */
background-color: var(--mud-palette-surface);
color: var(--mud-palette-text-primary);
height: var(--mud-appbar-height);

/* Avoid */
background-color: #282a2d;
color: white;
```

### `::deep` Selector

Use `::deep` to style child components from a parent's scoped CSS:

```css
::deep .mud-input {
    font-size: 0.875rem;
}
```

### Global Styles

`wwwroot/app.css` contains only essential global overrides (MudBlazor layout tweaks, Blazor error UI, loading indicator). Keep it minimal.

## Accessibility

- Use semantic MudBlazor components — they handle ARIA attributes.
- Provide `aria-label` on icon-only buttons.
- Ensure color contrast meets WCAG AA against the active theme palette.
- Support keyboard navigation for custom interactive components.

## Localization and Translations

- **Mandatory Translations**: Every new label, text, or user-facing string in a component or page MUST be localized.
- Do not hardcode any text strings in the .razor or .cs files.
- Use the IStringLocalizer service (or equivalent localization mechanism used in K7) to retrieve translated strings from .resx resources.
- When creating a new component or page, ensure its associated resource files (e.g., en, default) are updated with the new required keys.
