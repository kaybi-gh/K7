---
name: penpot
description: "Interact with a connected Penpot design project via the MCP plugin. Explore designs, read shape properties, create or modify shapes programmatically, and generate CSS/markup. Use when the user asks to inspect, edit, generate, or automate Penpot designs."
argument-hint: "[task description - what to read or create in the design]"
user-invocable: true
---

# Penpot MCP Skill

## Precondition

The user must have the Penpot MCP Plugin open and connected to a project. If they haven't, ask them to do so first.

Always call `mcp_penpot_high_level_overview` once at the start of a session to refresh your understanding of the API.

---

## Tool Reference

| Tool | When to use |
|---|---|
| `mcp_penpot_high_level_overview` | First call in any session; refreshes full API knowledge |
| `mcp_penpot_penpot_api_info` | Look up a specific type/interface (e.g. `Text`, `FlexLayout`) |
| `mcp_penpot_execute_code` | Run JS against the live design |
| `mcp_penpot_export_shape` | Export a shape as an image |

---

## Workflow: Explore First, Then Act

Never assume structure. Always explore before modifying.

```js
// 1. Get all pages
return penpotUtils.getPages();

// 2. Inspect a page's top-level structure (depth 3 is usually enough)
const page = penpotUtils.getPageByName("Components");
return penpotUtils.shapeStructure(page.root, 3);

// 3. Find a specific shape
const btn = penpotUtils.findShape(s => s.name === "Button/Primary", page.root);
return penpotUtils.shapeStructure(btn, 5);
```

Store intermediate results in `storage` to avoid re-fetching:

```js
storage.mainPage = penpotUtils.getPageByName("Main");
storage.hero = penpotUtils.findShape(s => s.name === "Hero", storage.mainPage.root);
return storage.hero ? "found" : "not found";
```

---

## Page Navigation is Asynchronous

`openPage()` (or any page-switch call) is **async in the Penpot plugin**. If you create shapes immediately after switching pages in the same code call, they will land on the **previous** page because the switch hasn't committed yet.

**Rule: always open the target page at the END of a call, not the beginning.**
The next call will then start already on the correct page.

```js
// WRONG - shapes created before the page switch lands
penpot.openPage(targetPage);
const board = penpot.createBoard(); // lands on the OLD page

// RIGHT - switch at the end so the next call starts on the right page
const board = penpot.createBoard(); // runs on current (already correct) page
// ... all modifications ...
penpot.openPage(nextTargetPage); // set up for the NEXT call
return "done";
```

Multi-page workflow pattern:

```js
// Call 1: create on Page A, end by opening Page B
// (assume we're already on Page A from a previous call or initial state)
const boardA = penpot.createBoard();
// ... populate boardA ...
penpot.openPage(pageB); // ready for next call
return "page A done";

// Call 2: now we're on Page B
const boardB = penpot.createBoard();
// ... populate boardB ...
penpot.openPage(pageC);
return "page B done";
```

---

## Code <-> Penpot Fidelity Rules

### Code -> Penpot

When translating Blazor components to Penpot designs:

- **Read all CSS before starting**: load `app.css`, every relevant `.razor.css` scoped file, and any global CSS. Never invent or guess values - use only what the code actually defines.
- **Use exact tokens**: colors, spacing, font sizes, border radii must come from the CSS variables and classes actually present in the codebase (`--color-*`, `--spacing-*`, etc.).
- **Reflect component structure**: when a Blazor component exists (e.g. `MediaCard`, `Sidebar`, `MiniMusicPlayer`), create a **Penpot component** (board marked as component/asset) to match it - not just a flat frame.
- **Respect scoped CSS scope**: a `.razor.css` file applies only to that component - don't apply its rules to unrelated shapes.
- **Preserve layout semantics**: if the code uses flexbox with `gap: 12px`, reproduce that with a flex layout board with `rowGap = 12` - don't manually space children.

### Penpot -> Code

When generating Blazor/CSS from a Penpot design:

- **Read the design first**: use `penpot.generateStyle` and `penpot.generateMarkup` to extract values - never eyeball or estimate.
- **Use existing K7 tokens**: if a color or spacing in the design matches a K7 CSS variable, use the variable - don't hardcode the raw hex.
- **Map Penpot components to Blazor components**: a Penpot component/asset corresponds to a Blazor component file - generate a `.razor` + `.razor.css` triad.
- **Scoped CSS only**: generated styles go in the component's `.razor.css` file, not in global CSS, unless the design explicitly shows it as a global/shared style.
- **Never invent interactions**: if the design doesn't show a hover state or animation, don't add one.

---

## Typography

K7 uses two fonts - both are available in this Penpot project:

| Font | Weights available | Role |
|---|---|---|
| **Epilogue** | 700, 900 | Headings, display text |
| **Manrope** | 400, 500, 700 | Body, UI labels, captions |

- Always use Epilogue for headings and Manrope for body/UI text - never substitute with Inter, Roboto, or system fonts.
- When going code -> Penpot: match the weight exactly to what the CSS specifies (`font-weight: 700` = Bold in Penpot).
- When going Penpot -> code: output `font-family: 'Epilogue'` or `font-family: 'Manrope'` - the fonts are self-hosted via libman and loaded through `app.css`.

---

## Icons

K7 uses **Phosphor Icons** exclusively - the same library is available as an asset in this Penpot project.

- Always use Phosphor icons from the Penpot asset library; never draw custom icon shapes.
- Match the weight used in the code: K7 defaults to the regular weight (`ph-*` classes without suffix). Use `ph-bold` / `ph-fill` variants only if the code explicitly does.
- When going code -> Penpot: look up the icon name in the source (e.g. `ph ph-devices`) and find the matching component in the Penpot Phosphor library.
- When going Penpot -> code: note the exact icon component name from the Penpot asset panel and use the corresponding `ph ph-<name>` class in the Blazor markup.

---

## Critical Gotchas

### Read-only vs writable properties

| Read-only (use methods instead) | Writable |
|---|---|
| `width`, `height` - use `resize(w, h)` | `x`, `y` (absolute page coords) |
| `bounds` | `name`, `opacity`, `rotation` |
| `parentX`, `parentY` - use `penpotUtils.setParentXY(shape, px, py)` | `fills`, `strokes`, `shadows` (replace full array) |

```js
// WRONG
shape.width = 200;

// RIGHT
shape.resize(200, shape.height);
```

### Fills - always replace the whole array

```js
// WRONG - individual fill objects are read-only
shape.fills[0].fillColor = "#FF0000";

// RIGHT
shape.fills = [{ fillColor: "#FF0000", fillOpacity: 1 }];

// Transparent (no fill)
shape.fills = [];

// Copy fills from another shape (safe - not shared references)
targetShape.fills = sourceShape.fills;
```

Colors must be hex with uppercase letters: `"#FF5533"`, not `"#ff5533"`.

### Text sizing

`resize()` sets `growType` to `"fixed"`, which causes text to overflow its box silently.
Always restore auto-sizing after resizing a text element:

```js
text.resize(300, 40);
text.growType = "auto-height"; // or "auto-width"
```

To read updated bounds after auto-resize, wait 100ms:

```js
text.growType = "auto-height";
await new Promise(r => setTimeout(r, 100));
return { w: text.width, h: text.height };
```

### Flex layout - NEVER use `board.flex.appendChild`

It is broken. Always use the board's own method:

```js
// WRONG
board.flex.appendChild(shape);

// RIGHT
board.appendChild(shape); // appends visually at the end
board.insertChild(0, shape); // inserts at specific index
```

When adding flex layout to a board that already has children, use the safe utility:

```js
// WRONG - arbitrarily reorders existing children
board.addFlexLayout();

// RIGHT - preserves current visual order
penpotUtils.addFlexLayout(board, "row"); // or "column"
```

### Z-order = children array order

Lower index = further back. Add background shapes first, foreground last.

```js
board.appendChild(background); // index 0, drawn first (bottom)
board.appendChild(foreground); // index 1, drawn on top
```

To reorder after creation:

```js
shape.bringToFront();
shape.sendToBack();
shape.setParentIndex(2); // 0-based
```

### Layout systems override x/y

If a board has flex or grid layout, setting `child.x` / `child.y` has no effect unless the child is marked absolute:

```js
child.layoutChild.absolute = true; // then x/y work relative to parent
```

To control spacing in flex layouts, modify gaps and padding on the layout, not individual child positions:

```js
board.flex.rowGap = 12;
board.flex.columnGap = 8;
board.flex.horizontalPadding = 16;
```

---

## Common Patterns

### Explore what the user has selected

```js
storage.sel = penpot.selection; // copy immediately - selection may change
return storage.sel.map(s => ({ id: s.id, name: s.name, type: s.type }));
```

### Create a styled rectangle

```js
const rect = penpot.createRectangle();
rect.name = "Card/Background";
rect.x = 100;
rect.y = 200;
rect.resize(320, 160);
rect.borderRadius = 12;
rect.fills = [{ fillColor: "#1A1A2E", fillOpacity: 1 }];
board.appendChild(rect);
return "created";
```

### Create a text element

```js
const t = penpot.createText("Hello World");
t.name = "Heading/H1";
t.fontSize = 32;
t.fills = [{ fillColor: "#FFFFFF", fillOpacity: 1 }];
t.growType = "auto-width";
board.appendChild(t);
return "created";
```

### Generate CSS for selected shapes

```js
return penpot.generateStyle(penpot.selection, { type: "css", withChildren: true });
```

### Generate HTML markup for a shape

```js
const shape = penpotUtils.findShape(s => s.name === "Card", penpot.root);
return penpot.generateMarkup([shape], { type: "html" });
```

### Find all text elements and list their content

```js
const texts = penpotUtils.findShapes(s => s.type === "text", penpot.root);
return texts.map(t => ({ name: t.name, content: t.characters }));
```

### Clone a shape and move it

```js
const original = penpotUtils.findShape(s => s.name === "Card/Template", penpot.root);
const clone = original.clone();
clone.name = "Card/Copy";
penpotUtils.setParentXY(clone, 0, 220); // position relative to parent
return "cloned";
```

---

## Don'ts

- **Never log data you also return** - it duplicates output and wastes context.
- **Never call `shape.remove()` for reparenting** - use `newParent.appendChild(shape)` instead.
  (`remove()` is permanent and on component descendants only hides the shape.)
- **Never hardcode page names** - always call `penpotUtils.getPages()` first to confirm names.
- **Never write custom shape-search loops** - use `penpotUtils.findShape()` / `findShapes()`.
- **Never re-implement `setParentXY`** - the utility already handles the coordinate math.
