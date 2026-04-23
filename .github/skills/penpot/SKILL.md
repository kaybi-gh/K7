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

## Variant Components

### Naming convention

Penpot parses variant properties directly from the board name using the format `Property=Value, Property2=Value2`.

```
K7Button / Variant=primary, State=default
K7Button / Variant=primary, State=hover
K7Button / Variant=primary, State=disabled
```

- The prefix before ` / ` becomes the component group name.
- Each `Property=Value` pair maps to a variant property.
- A property with exactly two values named `yes/no`, `true/false`, or `on/off` renders as a **boolean toggle** in the inspector instead of a dropdown.

### Discovering variant structure

```js
// Check all VariantContainers on the current page
const page = penpot.currentPage;
const shapes = page.findShapes();
const vcs = shapes.filter(s => s.isVariantContainer && s.isVariantContainer());
return vcs.map(vc => ({
  name: vc.name,
  children: Array.from(vc.children).length,
  x: Math.round(vc.x),
  y: Math.round(vc.y)
}));
```

```js
// Read all variant property values for a component group
const local = penpot.library.local;
const comp = local.components.find(c => c.name === "K7Button" && c.isVariant && c.isVariant());
const vg = comp.variants; // VariantGroup
return {
  properties: vg.properties,
  allVariants: vg.variantComponents().map(v => v.variantProps)
};
```

### VariantGroup API

`comp.variants` returns a `VariantGroup` object with these **own properties** (not visible in `Object.keys` - they are on the JS prototype, use `Object.getOwnPropertyNames`):

| Property / Method | Description |
|---|---|
| `vg.properties` | Array of property name strings, e.g. `["Variant", "State"]` |
| `vg.variantComponents()` | **Function** - call it to get the array of all variant `LibraryComponent` objects |
| `vg.addVariant()` | Duplicates the current component into a new variant (auto-named `Value N`) |
| `vg.addProperty(name)` | Adds a new property to all variants (value defaults to `Value 1`) |
| `vg.renameProperty(index, newName)` | Renames property at 0-based index - use **index**, not the old name |
| `vg.removeProperty(index)` | Removes a property by index |

### Individual variant API (`LibraryVariantComponent`)

```js
const allV = vg.variantComponents(); // always call, never iterate vg.variants directly
const v = allV[0];

v.variantProps          // { "Variant": "primary", "State": "default" }
v.setVariantProperty(propIndex, value) // set value by 0-based property index
v.mainInstance()        // returns the canvas VariantHead board for direct styling
v.remove()              // removes this variant and its canvas board
v.addVariant()          // same as vg.addVariant(), duplicates this variant
```

**Critical**: `vg.variantComponents` is a **function**, not a property. Always call `vg.variantComponents()`.

### Renaming existing `Property 1` to a semantic name

After `combineAsVariants()`, all groups start with generic `Property 1`. Rename it before adding new variants:

```js
const vg = comp.variants;
vg.renameProperty(0, "Variant"); // use index 0, NOT the string "Property 1"
vg.renameProperty(1, "State");
```

### Styling variant canvas boards

The name setter on VariantHead boards silently fails - always style via `v.mainInstance()`:

```js
const local = penpot.library.local;
const comp = local.components.find(c => c.name === "K7Button" && c.isVariant && c.isVariant());
const vg = comp.variants;
for (const v of vg.variantComponents()) {
  const board = v.mainInstance(); // returns the canvas VariantHead shape
  if (!board) continue;

  // Set fill
  board.fills = [{ fillColor: "#E5A00D", fillOpacity: 1 }];

  // Set opacity (disabled state)
  board.opacity = 0.45;

  // Set stroke (focused state)
  board.strokes = [{ strokeColor: "#E5A00D", strokeWidth: 1.5, strokeStyle: "solid", strokeAlignment: "inner", strokeOpacity: 1 }];
}
```

Alternatively, read canvas children from the VC shape directly using their index. Fills/opacity setters DO work when accessed via `vc.children` array - the issue only affects orphaned clone references:

```js
const vc = shapes.find(s => s.name === "K7Button" && s.isVariantContainer && s.isVariantContainer());
const children = Array.from(vc.children);
children[6].fills = [{ fillColor: "#FCB00E", fillOpacity: 1 }]; // works
children[6].opacity = 0.45; // works
```

### Converting a standalone component to a variant

Use `comp.transformInVariant()` on the `LibraryComponent`. This creates a VariantContainer with a single variant named `Value 1`.

```js
const local = penpot.library.local;
const comp = local.components.find(c => c.name === "K7Select");
comp.transformInVariant(); // now comp.isVariant() === true

const vg = comp.variants;
vg.renameProperty(0, "State");           // rename generic property
comp.setVariantProperty(0, "default");   // set initial value

// Add more variants
comp.addVariant(); // returns empty - but variant IS created
// Get it from variantComponents()
const allV = vg.variantComponents();
allV[1].setVariantProperty(0, "focused");
allV[2].setVariantProperty(0, "error");
allV[3].setVariantProperty(0, "disabled");
```

### Creating a new multi-property variant component from scratch

```js
// 1. Create the first board
const board = penpot.createBoard();
board.name = "K7ToggleButton";
board.x = 9;
board.y = 6000;
board.resize(120, 36);
board.borderRadius = 8;
board.fills = [{ fillColor: "#E5A00D", fillOpacity: 1 }];

// Add a text label
const label = penpot.createText("Label");
label.fontFamily = "sourcesanspro";
label.fontSize = 14;
label.fills = [{ fillColor: "#000000", fillOpacity: 1 }];
board.appendChild(label);

// 2. Register as a component - use createComponent([shape]) (array, not bare shape)
const local = penpot.library.local;
const comp = local.createComponent([board]); // correct: array of shapes

// 3. Transform to variant and configure properties
comp.transformInVariant();
const vg = comp.variants;
vg.renameProperty(0, "Selected");
vg.addProperty("State");       // adds "Property 2", rename it:
vg.renameProperty(1, "State");

// 4. Set initial variant values
comp.setVariantProperty(0, "yes");     // Selected=yes
comp.setVariantProperty(1, "default"); // State=default

// 5. Add remaining variants
comp.addVariant(); // creates Selected=yes, State=Value 2 - fix with setVariantProperty
const allV = vg.variantComponents();
allV[1].setVariantProperty(0, "yes");
allV[1].setVariantProperty(1, "hover");
// ... repeat for all combinations

// 6. Style each canvas board via mainInstance()
for (const v of vg.variantComponents()) {
  const inst = v.mainInstance();
  inst.fills = [...];
  inst.opacity = v.variantProps.State === "disabled" ? 0.45 : 1;
}
```

### Removing unwanted variants

`addVariant()` creates one variant per call but does not return a useful reference. Always fetch the result via `vg.variantComponents()` after the call. Remove extras with `v.remove()`:

```js
const allV = vg.variantComponents();
// Remove any variant with a generic auto-assigned value
const bad = allV.find(v => Object.values(v.variantProps).some(val => val.startsWith("Value ")));
if (bad) bad.remove();
```

### Combine multiple boards as variants

`shape.combineAsVariants(ids[])` requires that the boards are already registered as components AND share the same component name prefix (the part before ` / `). Use `transformInVariant` + `addVariant` instead for programmatic workflows - it is more reliable.

---

## Library Components

### Creating library components programmatically

Use `penpot.library.local.createComponent(shapes[])` to register boards as reusable library components.
The method exists on the `Library` interface but is NOT visible in `Object.keys()` enumeration (it is on the prototype).

```js
// Register a single board as a component
const board = penpotUtils.findShape(s => s.name === "K7Button / primary", page.root);
const comp = penpot.library.local.createComponent([board]);
// comp.id, comp.name are now available

// Register all top-level boards on the current page as components
const page = penpot.currentPage;
const shapes = page.findShapes();
const topLevelBoards = shapes.filter(s =>
  s.type === "board" &&
  s.parent &&
  s.parent.name === "Root Frame" &&
  s.name !== "Root Frame"
);
const local = penpot.library.local;
const created = [];
for (const board of topLevelBoards) {
  const comp = local.createComponent([board]);
  created.push(board.name);
}
return { count: created.length, names: created };
```

Key facts:
- `createComponent([board])` takes an **array of shapes** that form the component contents.
- Top-level boards have `shape.parent.name === "Root Frame"` (the root frame is itself a board named "Root Frame").
- After creation, components appear in **Assets → Local library → Components** in the Penpot UI.
- Verify with `penpot.library.local.components.length`.

---

## Don'ts

- **Never log data you also return** - it duplicates output and wastes context.
- **Never call `shape.remove()` for reparenting** - use `newParent.appendChild(shape)` instead.
  (`remove()` is permanent and on component descendants only hides the shape.)
- **Never hardcode page names** - always call `penpotUtils.getPages()` first to confirm names.
- **Never write custom shape-search loops** - use `penpotUtils.findShape()` / `findShapes()`.
- **Never re-implement `setParentXY`** - the utility already handles the coordinate math.
