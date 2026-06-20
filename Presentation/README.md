# Presentation: Local AI Agents Are Powerful, But Not Reliable by Default

A standalone, browser-based slide deck explaining how Book Notes IA turns a local Ollama model into a
reliable agent: Ollama, Microsoft Agent Framework (MAF), PostgreSQL + pgvector, embeddings, token
counting, and .NET working together.

This module has no build step and no dependency on the rest of the repository. It is plain HTML/CSS/JS
plus a few CDN libraries, styled to match the WebApp's warm, dark, serif visual language (Zilla Slab +
Crimson Text, the sepia Shoelace palette, 5px corner radius).

## How to open it

Just open the file directly in a browser:

```bash
open Presentation/index.html        # macOS
xdg-open Presentation/index.html    # Linux
```

Everything (fonts, Shoelace, Tailwind, Mermaid, ECharts) loads from CDN over `https://`, so opening the
file with `file://` works in any modern browser — no CORS issues, since nothing here uses ES module
imports or `fetch()` against relative paths.

If your browser ever blocks local script loading for some other reason, serve the folder instead:

```bash
cd Presentation && python3 -m http.server 8088
# then open http://localhost:8088
```

An internet connection is required the first time, to fetch the CDN assets.

### Single-file bundle

If you want one file you can copy or send around instead of the whole folder, generate a self-contained
bundle:

```bash
make presentation-bundle
```

This runs `Presentation/build.js` inside a throwaway `node:22-alpine` Docker container (no local Node/npm
install needed — same Docker-first approach as the rest of this project) and writes
`Presentation/dist/presentation.html`, with `styles.css`, `charts.js`, `presentation.js`, and
`assets/*.png` inlined directly into the HTML and the whole file minified via `html-minifier-terser`. Open
just that one file. CDN-hosted libraries (fonts, Shoelace, Tailwind, Mermaid, ECharts) and the hardware
slide's VRAM-calculator iframe are intentionally left as external `https://` references, so an internet
connection is still needed on first load; bundling those would mean vendoring entire third-party libraries.
Everything build-related (`build.js`, `package.json`) lives under `Presentation/` itself — nothing was
added to the repo's root `Scripts/` folder. The bundle and `node_modules/` are generated artifacts
(gitignored, regenerate any time after editing the source files) — they don't replace `index.html`, which
remains the file to edit.

## Navigating

| Action | Keys / control |
| --- | --- |
| Next slide | `Space`, `→`, `Page Down`, click the right circular button, swipe left |
| Previous slide | `←`, `Page Up`, click the left circular button, swipe right |
| Jump to a slide | click a dot in the bottom-left progress dots |
| Deep link | the URL hash tracks the current slide (`#slide-5`); reload to resume there |

The top bar shows a slide counter and a thin progress bar. There is no slide-count limit on navigation —
both ends simply stop advancing past the first/last slide.

## Where things live

- `index.html` — an unnumbered cover slide (title + author byline) followed by all 14 numbered content
  slides, in order, each a `<section class="slide">`. The on-screen page counter is 0-indexed to match —
  the cover shows "00", and each content slide's number matches its own `kicker` label (e.g. the slide
  kickered "01" shows "01 / 14"). Slide content (text, cards,
  Mermaid source, chart containers) lives directly in the markup for that slide.
- `styles.css` — the visual system: CSS variables for the warm sepia palette (lifted from
  `WebApp/Styles/_colors.sass` and `_variables.sass`), the slide engine (fade/rise transitions), card and
  code-panel styles, and chart/diagram containers.
- `presentation.js` — the slide engine: keyboard/click/swipe navigation, progress bar, dot indicators,
  hash sync. Framework-free.
- `charts.js` — Mermaid initialization/theme (matched to the same palette) and ECharts chart builders.
  Diagrams render lazily the first time their slide becomes active; charts initialize lazily and resize
  on every slide entry and on window resize.

## Editing

- **Add or edit slide text/cards**: edit the relevant `<section class="slide">` block in `index.html`.
  Keep the `kicker` / `slide-title` / `slide-subtitle` structure for consistent styling.
- **Add or edit a Mermaid diagram**: edit the `<pre class="mermaid">...</pre>` block. The `<pre>` tag
  preserves the newlines Mermaid's flowchart/subgraph syntax depends on — don't switch it to a plain
  `<div>` without checking multi-line diagrams still parse.
- **Add or edit an ECharts chart**: add a `<div id="chart-foo" data-echarts></div>` in `index.html`, then
  add a matching `'chart-foo': () => baseOption({...})` entry to the `builders` map in `charts.js`. The
  engine wires it up automatically the first time that slide is shown.
- **Charts that need the container's pixel size** (like `chart-model-sizes`'s nested squares): the builder
  receives the live `chart` instance as its argument — call `chart.getWidth()` / `chart.getHeight()` to
  compute pixel-accurate `graphic` element positions. The engine re-invokes every chart's builder (not just
  `resize()`) on window resize and on re-entering its slide, so size-dependent graphics stay correct.
- **Change the palette/fonts**: edit the `:root` variables at the top of `styles.css`.

## Assumptions made

- Token-counting and context-ring concepts (`TokenAccumulator`, `MaxPromptTokens`, `ContextUsagePct`,
  `Ollama:NumCtx = 8192`) are described as they exist today in `WebApp/Services/TokenCountingChatClient.cs`
  and the `context-token-awareness` / `context-pressure-metrics` specs.
- **Summarization is not yet implemented.** Both specs above explicitly list "rolling summarisation" as
  out of scope. The context/summary tradeoff slide presents it as a deliberate next step with illustrative
  numbers, and says so directly, rather than implying it already ships.
- The "Jennifer Government" slides use real captured local-session transcripts (failed attempts without
  retrieval, then a grounded success once embeddings + `GenerateBookContext` + Open Library synopsis
  enrichment are wired in) rather than invented examples.
- The hardware slide's "This machine" panel (inside the **Today** card) reflects this device's actual
  `lscpu` / `free -h` / `/sys/class/drm/*/device/mem_info_*` output at the time it was written, not an
  estimate.
- The AMD/NVIDIA comparison cards use real, web-verified specs (current as of this writing) for the
  **AMD Ryzen AI Max+ 395** ("Strix Halo": 128 GB unified LPDDR5x, ~96 GB allocatable as GPU VRAM on
  Windows / ~120 GB on Linux) and **NVIDIA DGX Spark** (GB10 Grace Blackwell Superchip: 128 GB unified
  LPDDR5x, up to 1 PFLOP FP4, inference up to ~200B params). The card tags were corrected from an earlier
  draft's "AMD Ryzen AI+" / "NVIDIA RTX Spark" — the latter isn't a real product name.
- **Photo/SKU mismatch**: `assets/amd.png` is a real Ryzen AI chip photo, but the text baked into the image
  itself reads "Ryzen AI **PRO 300 Series**" — a different, non-Max SKU that does not have the large
  VRAM-for-LLM story the card's spec list describes (that story belongs to the Max+ 395 / Strix Halo SKU).
  The photo is kept as a representative Ryzen AI chip image; the spec numbers next to it are specifically
  about the Max+ 395. `assets/nvidea.png` is a real isolated NVIDIA chip die photo and needs no caveat.
- These comparison cards are general directional context, not a benchmark, pricing claim, or endorsement.
- The closing VRAM bar chart compares GPU-addressable memory across this Legion Go S unit (measured, see
  above), two current discrete gaming GPUs (NVIDIA RTX 5090: 32 GB GDDR7; AMD Radeon RX 7900 XTX: 24 GB
  GDDR6 — chosen over the newer RX 9070 XT because it has more VRAM), a Mac mini at its M4 Pro max
  configuration (64 GB unified memory — the currently shipping chip; M5 Mac mini specs were still rumor-only
  at the time of writing), and the two unified-memory platforms from the cards above. The "≈ N B params"
  tooltip figure is an illustrative Q4-quantization rule of thumb (`GB &times; 1.25`), not a real benchmark.

## TODOs intentionally left

- Replace `assets/amd.png` with an actual Ryzen AI **Max+ 395** chip photo if/when one is available, to
  remove the SKU mismatch noted above.
- If summarization ships, update the context/summary tradeoff slide's caption to drop the "deferred"
  framing and link the real implementation.
