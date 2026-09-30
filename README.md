# NO UNIT 404 — greybox vertical slice

Unity 6000.3.2f1 · URP · Windows

This is the playable build described in GDD sections 9 (scenario), 10 (endings), 20 (technical
design) and 37/39 (start-up order): the full system layer plus **all seven shifts and all five
endings** running on a procedural greybox. **World art and characters are deliberately
placeholder** — primitives and capsules — so they can be replaced without touching gameplay
code. Audio, the UI font, the app icon and the Steam integration are real and ship as they are.

---

## Getting started

1. Open the project in Unity **6000.3.2f1**.
2. Run **Tools ▸ NO404 ▸ Setup ▸ Create Entry Scene And Build Settings** (once).
   This creates `SCN_Bootstrap`, registers it as build scene 0 and applies the product settings.
3. Press **Play**. The main menu appears; choose *새 게임 / New game*.

`Bootstrap` installs itself with `[RuntimeInitializeOnLoadMethod]`, so pressing Play from any
open scene also works — step 2 only matters for builds.

Nothing else needs wiring: services, the greybox world, the player and every UI screen are
created in code at runtime.

### Menu items

| Menu | What it does |
|---|---|
| Tools ▸ NO404 ▸ Setup ▸ Create Entry Scene And Build Settings | One-time project setup |
| Tools ▸ NO404 ▸ Data ▸ Validate Content | Checks ids, dialogue links, loc keys, fail-safes |
| Tools ▸ NO404 ▸ Data ▸ Bake Seed Content To Assets | Converts code content into editable assets |
| Tools ▸ NO404 ▸ Data ▸ Revert To Code Seed Content | Deletes the catalog, back to the code seed |
| Tools ▸ NO404 ▸ Build ▸ Windows Development Build | `Builds/Development_<stamp>/NO_UNIT_404.exe` |
| Tools ▸ NO404 ▸ Build ▸ Windows Release Build | Release variant |
| Tools ▸ NO404 ▸ Delete All Saves | Clears every save slot |

Tests: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.

---

## Controls (GDD 8.1)

| Action | Key |
|---|---|
| Move / look | WASD / mouse |
| Interact | E (hold where the prompt shows a bar) |
| Sprint / crouch / flashlight | Shift / C / F |
| Duty tablet | Tab |
| Evidence board (while at the PC) | Q |
| Quick CCTV snapshot (while on the CCTV app) | R |
| Pause | Esc |
| Developer console (editor & dev builds) | ` (backquote) |

Leave the facility PC with Esc.

---

## What is playable

All fifteen main cases C00–C14 (GDD 11.3), all eighteen routine tasks (GDD 11.4),
all thirty-six CCTV anomaly types (GDD 12.3) and all five endings (GDD 10).

The front door runs **55 callers across the six shifts** - six to eleven a night, one or two of
whom should be turned away (GDD 13.4). The volume is the teaching: nobody recognises a wrong
caller without having seen enough right ones.

### Dimensions (GDD 17.4 / 17.5)

| | |
|---|---|
| Corridor width · wall height | **1.55m** · **2.6m** |
| Unit door · fire door | 0.9 × 2.05m · 1.0 × 2.1m |
| Stair flight · plan grid | 1.25m · 0.5m |
| Office · parking · unit 404 | 8×6m · 25×18m · 18m² |

The one deliberate exception: on the fourth floor the wall between 403 and 405 is 1.2m wider
than the same wall anywhere else, which puts it off the 0.5m grid. That is the point — it is
the room that was built there.

### Spaces and streaming

Office · lobby · parking · **elevator car** (a real zone, so its indicator can read 16) ·
**stairwell** (the route that survives the night-5 outage) · basement records room ·
4F/8F/13F corridors · **4F service passage** · rooftop · unit 404.

Zones are split across **additive scenes** (GDD 20.5). `SCN_Core` (office, lobby, elevator,
stairwell) is always resident; the rest stream. A door preloads its destination while you are
looking at it and refuses to open until the load finishes, a failed load leaves the door shut
with a retryable message, and watching a camera is what keeps that floor in memory. Real
loading shows "connecting"; the horror `SIGNAL LOST` is a separate state.

### The two threat windows

GDD 15.2 forbids being chased on an ordinary patrol, so there are exactly two:

- **Night 5, parking and records room** — a patrol on an authored route, not a loop
  (`Threat/PatrolRoutes.cs`). In the parking he comes down the stairs, checks the breaker
  panel, sweeps the south fire lane past the unregistered car and tries the archive door; in
  the records room he works the shelves and the server rack, stopping at each, because GDD 9.6
  has him there to take records back and a 6×5 room walked without stopping puts him at the
  one hiding cabinet every eight seconds. Routes and props read the same table
  (`Gameplay/BasementLayout.cs`) and `PatrolRouteTests` holds every leg against it. He moves at
  85–95% of your sprint (the exact figure is the difficulty option), crouching roughly halves
  how fast he notices you, and three hiding spots light up while the window is open. Being
  caught is never death: the chairman takes one non-critical piece of evidence, doctors your
  performance and costs you ten minutes.
- **Night 6, after the confrontation** — the fire. Nothing chases you; the building does. You
  have fifteen game minutes to reach the lobby, and if `BuildingSafety` is 40 or lower part of
  the stairwell is gone and you have to take the elevator. With a high `HarinResonance` the
  emergency lights come on along the route that still works. Running out of time costs the
  original ledger, not the run.

### Difficulty and accessibility

Every switch in the options does something, and there is a test for each:

| Option | Effect |
|---|---|
| Difficulty (GDD 24.2) | pursuit speed 75/90/95%, routine deadlines ×1.5/×1/×0.8, hint delay, CCTV nagging |
| Hints (GDD 24.3) | off / delayed / always, escalating at 3-6-9 min: restate → app or zone → record name → exact menu path. Two wrong calls in a row start it a rung higher (GDD 24.1) |
| Subtitles (GDD 16.17) | speech as `[박동식] …`, ambient cues as `[엘리베이터가 위층에서 멈춘다]`, separate switches, 90–160% size |
| Colour-blind | graph series get a glyph, evidence links carry colour **and** thickness **and** a written relation |
| Reduce motion / camera shake | head bob and shake |
| Remove choice timers · easier pursuit | override the difficulty; an access need outranks the setting |
| Streamer mode · brightness | lift the lighting floor rather than washing the image out |

---

## Architecture

```
Assets/_Project/
├─ Resources/NO404/strings.csv     ko/en string table (single source of user-visible text)
├─ Scripts/
│  ├─ Core/        Bootstrap, ServiceHub, GameLoop, EventBus, GameClock, state, save-adjacent services
│  ├─ Cases/       Case state machine, conditions, objectives, decisions, consequences, fail-safes
│  ├─ CCTV/        Channel logic, anomalies, rewind, snapshots + the render rig
│  ├─ Content/     ContentDatabase and the authored seed content
│  ├─ Dialogue/    Node-graph runner shared by radio, phone and interphone
│  ├─ Endings/     Ending definitions, selection and the gallery
│  ├─ Evidence/    Inventory and board graph
│  ├─ Facility/    Meter series and the night-5 circuit budget
│  ├─ Phone/       Incoming calls, priority queue, missed-call handling
│  ├─ Gameplay/    First-person controller, input, greybox world, elevator, zone streaming
│  ├─ Interaction/ IInteractable, raycaster, doors, pickups, zone transitions
│  ├─ Residents/   Resident directory, access log and travel-time comparison
│  ├─ Save/        Atomic checksummed saves, slots, schema migration
│  ├─ Threat/      The night-5 patrol, hiding spots and the cost of being caught
│  ├─ UI/          Placeholder uGUI: HUD, facility OS, tablet, dialogue, menus, dev console
│  └─ Editor/      Setup, build, content validation, content baking
├─ Tests/EditMode/ Clock, bus, saves, content, endings, phone, spec, difficulty, hints,
│                  captions, fire escape
└─ Tests/PlayMode/ Zone streaming: load, group contents, eviction, and a walkable corridor
```

Service start-up order follows GDD 20.6. `ServiceHub` is a flat registry, not a DI container.

### Time scale (GDD 6.2)

patrol `1.0` · facility OS `0.65` · tablet `0.5` · conversation `0.25` · evidence board and
pause `0.0`.

### Saves

`%USERPROFILE%\AppData\LocalLow\ProjectCaretaker\NO UNIT 404\saves\` — slots 0–2 rotating
autosaves, 3 manual, 4 night-start backup. Each file is `<checksum>\n<json>`, written to a
temp file and swapped in. A corrupt file falls back to the next newest slot; run
`save.corrupt_test` in the dev console to exercise that path.

---

## Placeholders that the art/audio pass replaces

- **World**: primitives from `Gameplay/WorldBuilder.cs`, every dimension derived from
  `Gameplay/BuildingSpec.cs` (GDD 17.4) and the per-space sizes in GDD 17.5 — 1.55m corridors,
  2.6m walls, 0.9x2.05 unit doors, 1.0x2.1 fire doors, a 0.5m plan grid, an 8x6 office, a
  25x18 parking section and 18m² for unit 404. `BuildingSpecTests` fails the build if any of
  that drifts, so swapping in real modules is a swap, not a re-layout.
- **UI**: code-built legacy uGUI (`UiFactory`), now drawing with an embedded Pretendard face
  (`Resources/NO404/Fonts`, SIL OFL 1.1) rather than an OS font. When real art arrives, swap
  `UiFactory` widgets for TMP + sprites — the views only call into it, they do not draw
  anything themselves.
- **Audio**: synthesised, not recorded. `Tools/GenerateAudio.py` renders all 19 cues to
  `Resources/NO404/Audio`; dropping a real recording over any file needs no code change.
  The loudness policy in GDD 19.3 is enforced by `AudioService` and held by `AudioPolicyTests`.
- **Steam**: fully wired against Steamworks.NET, but still on **app id 480** (Valve's test
  app) and no achievement is registered on the partner site. See `Docs/Release/STEAM.md`.
- **CCTV cameras 10–12** currently sit in the parking and rooftop greyboxes rather than in a
  dedicated archive corridor and recycling area; those spaces are built in the art pass.

## Shipping

The work that is neither code nor art, in `Docs/Release/`:

| File | What it answers |
|---|---|
| `RELEASE_CHECKLIST.md` | Everything still outstanding, who owns it, and what has a lead time |
| `ART_BRIEF.md` | Exact specs for the art and audio pass — sizes, paths, and what the tests enforce |
| `STORE_PAGE.md` | Store copy in ko/en, tags, and the pre-publish checks |
| `STEAM.md` | The partner-site settings that the code is already keyed to |
| `RATING_GRAC.md` | Content description for the Korean rating submission |
| `LICENSING.md` | Unity Personal terms, the splash screen, and third-party notices |

`Tools ▸ NO404 ▸ Release ▸ Release Readiness Report` is the machine-checkable half of that
checklist. `ReleaseSetup.ReportForCi` is the same check with a non-zero exit code.

## Known gaps against the GDD

These are scoped-out on purpose and are the natural next issues:

- Audio (GDD 19) is synthesised rather than recorded, and there is no music (19.4) or
  voice (19.5) at all.
- Steam (GDD 20.19) runs against Valve's test app id until a real one exists. The id lives in
  `Core/SteamAppInfo.cs`; the readiness report treats the placeholder as BLOCKING.
- The performance budget (GDD 20.20) can now be measured — `Tools ▸ NO404 ▸ Release ▸
  Measure Performance` — but never on the GDD 20.2 minimum spec.
- Localization currently uses the lightweight CSV service. Unity Localization can replace it by
  reimplementing `LocalizationService` only — the `Loc.T(key)` call sites do not change.
- Japanese and Simplified Chinese columns (P1). Add columns to `strings.csv`; the parser picks
  up whatever the header declares.
