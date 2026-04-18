---
applyTo: "src/Clients/**"
---

# Blazor UI Instructions

## Shared Component Architecture

All reusable UI code lives in `Clients/Shared/`, split into four projects:

| Project | Role |
|---|---|
| `Clients/Shared/Components` | Reusable Blazor components |
| `Clients/Shared/Pages` | Routable pages shared between Web and MAUI |
| `Clients/Shared/Services` | Client-side services (API access, state, theming) |
| `Clients/Shared/Models` | Client-side domain models and DTOs |

Both `Clients/Web` (Blazor WASM) and `Clients/MAUI` (Blazor Hybrid) reference these shared projects.

## Component Authoring

Use the **triad pattern** (up to three files per component):

```
MyComponent.razor       ← Markup
MyComponent.razor.cs    ← Code-behind (logic, parameters, inject)
MyComponent.razor.css   ← Scoped CSS (optional)
```

```csharp
// Good: Code-behind with partial class
public partial class MyComponent : ComponentBase
{
    [Inject] private IMyService MyService { get; set; } = default!;
    [Parameter] public string Title { get; set; } = string.Empty;
}

// Avoid: All logic inline in .razor file
```

## Placement Rules

- New **pages** (routable) → `Clients/Shared/Pages/`
- New **reusable components** → `Clients/Shared/Components/`
- New **client services** → `Clients/Shared/Services/`
- **MAUI-specific** code (platform APIs, native services) → `Clients/MAUI/` only

## K7 Component System

K7 has its own component library under `Clients/Shared/UI/Components/`. Use K7 components directly in pages; do not use third-party UI frameworks in pages.

When building new reusable components, wrap K7 primitives:

```razor
@* Good: K7-specific wrapper component *@
<K7MediaCard Title="@Media.Title" ImageUrl="@Media.PosterUrl" />

@* Avoid: third-party components directly in pages *@
```

## Theming

- Use `ThemeService` and `Themes.cs` for theme definitions.
- Never hardcode colors; use CSS variables.
- Theme providers are centralized in `App.razor`.

## CSS Conventions

- Prefer **scoped CSS** (`.razor.css`) over global styles.
- Use **CSS variables** for theming values.
- No inline styles (`style="..."`) ; use CSS classes.
- If custom CSS is needed outside scoped files, place it in the relevant project's `wwwroot/` folder.

## Localization and Translations

- **Mandatory Translations**: Every new label, text, or user-facing string in a component or page MUST be localized.
- Do not hardcode any text strings in the .razor or .cs files.
- Use the IStringLocalizer service (or equivalent localization mechanism used in K7) to retrieve translated strings from .resx resources.
- When creating a new component or page, ensure its associated resource files (e.g., en, default) are updated with the new required keys.
