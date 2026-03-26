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

Use the **triad pattern** — up to three files per component:

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

## MudBlazor

MudBlazor is the current UI component framework. When building reusable K7-specific components, **wrap MudBlazor** rather than using it directly in pages:

```razor
@* Good: K7-specific wrapper component *@
<K7MediaCard Title="@Media.Title" ImageUrl="@Media.PosterUrl" />

@* Avoid: MudBlazor directly in every page *@
<MudCard Class="ma-4">
    <MudCardMedia Image="@Media.PosterUrl" />
    <MudCardContent><MudText>@Media.Title</MudText></MudCardContent>
</MudCard>
```

This future-proofs against a potential framework swap.

## Theming

- Use `ThemeService` and `Themes.cs` for theme definitions.
- Never hardcode colors — use MudBlazor theme tokens or CSS variables.
- Theme providers are centralized in `CustomMudProviders.razor`.

## CSS Conventions

- Prefer **scoped CSS** (`.razor.css`) over global styles.
- Use **CSS variables** for theming values.
- No inline styles (`style="..."`) — use CSS classes.
- If custom CSS is needed outside scoped files, place it in the relevant project's `wwwroot/` folder.
