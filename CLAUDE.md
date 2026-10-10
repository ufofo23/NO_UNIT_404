# NO UNIT 404 — Development Rules

## Product

- 404호는 없습니다 / NO UNIT 404 (internal codename `ProjectCaretaker`)
- First-person facility-management simulation + psychological horror + investigation, Windows / Steam
- Unity 6000.3.2f1, URP, C# (compiled as C# 9 — no `record struct`, no file-scoped namespaces)

## Source of truth

- Design: `NO_UNIT_404_GDD_v5.1.md` in this folder is **the current top of the stack**
  (solo first, 15-quest night pools, B1~6F, single ending). Where it disagrees with anything
  below, v5.1 wins. What of it is built for nights 1/3/5 is in `Docs/NIGHTS_1_3_5_V51.md`
  and `Docs/MAIN_QUESTS_1_3_5.md`. The older documents below are no longer in the working
  tree; they are described here for the systems that were built against them.
  - `NO_UNIT_404_MASTER_IMPLEMENTATION_GDD_v3.0.md` — **the top of the stack.** Where anything
    disagrees with it, v3.0 wins. It re-aims the product at **1–4 player co-op** and rewrites
    the visitor system into Access & Pursuit (sections V3-G and 38): the door grants one of
    seven access levels rather than opening or refusing, and a granted visitor keeps acting
    inside the building afterwards. Its own FINAL RULE B-02 forbids ending a P0 visitor event
    at the button.
  - `NO_UNIT_404_GDD_v2.1_MANUAL_ANOMALY_SPEC.md` — still authoritative on the building
    layout, floor and stair movement, the night response manual, the risk model, and the
    M01–M18 / A01–A05 anomaly content, except where v3.0 restates them.
  - `NO_UNIT_404_GDD_v1.0.md` — the base design. The file name still says v1.0; the document
    inside is at v2.3 and carries its own changelog in section 0.3. The GDD calls for
    `Docs/GDD/NO_UNIT_404_GDD.md`; the file has not been duplicated so that there is exactly
    one copy to keep in sync. Update the path here if it ever moves.

### What of v3.0 is built, and what is not

The gap matters more than the plan, so it is written down here rather than assumed.

- **Built:** the Access & Pursuit system — seven access levels (`VisitorAccessLevel`),
  revocable `AccessToken`, `ActiveVisitorService` walking granted visitors along an expected
  route, observation-only position tracking, off-route detection, escort, and the tracking
  board on the home screen. Plus the read layer under it (`DoorReadService`) that v3.0 38.3
  calls Layer C and Layer D.
- **Built, co-op:** the 1–4 player session layer and a shift that three people can play
  through. `MultiplayerBootstrap` / `MultiplayerSessionService` open a Relay room and a join
  code; `NetSession` owns the NetworkManager and the roster; `NetPlayer` is one caretaker
  (body, zone, and what they are drawing off the reserve); `NetShift` is the shift, in three
  layers described in `NetShift.Night.cs`:
    - **state** — `ShiftMirror` sends the host's save file, compressed, on change, and
      `SaveService.ApplyShiftMirror` applies the shift half of it. Anything a save records is
      therefore mirrored: cases, objectives, evidence, stats, flags, reserve, pressure, the
      access log, manual events. **A service that starts being saved starts being mirrored —
      do not add a parallel sync for it.**
    - **moments** — `NetShift.Broadcast` for the handful of things a mirror half a second
      later would ruin: a knock, a caption, a notice, an anomaly reaching a lens, a dialogue
      node, the end of the night.
    - **actions** — `NetShift.Request(ShiftAct...)` is the one funnel every verb that changes
      the shift goes through. It runs locally when authoritative and asks the host otherwise,
      so **no call site in the game contains an "am I networked" test.** Add a verb here, not
      an `if`.
  The host simulates alone: `GameLoop.TickShift` and every scheduler under it run only where
  `NetSession.Authoritative` is true.
- **Not built, co-op:** nights 2–6 have had no three-handed pass — only night 1 is
  deliberately complete. No host migration and no reconnect: a session is one sitting. The
  stalker (`ThreatService`, night 5+) is host-only and has no client presentation. Sections
  V3-C, 47–49 remain unimplemented.
- The building is `B1, 1F..6F` and nothing else (v5.1 3.1). There is no second basement, no
  roof and no thirteenth floor: `FloorPlan` has no row for them, so no stair or lift code can
  resolve one. The plant and records rooms are on B1.
  Add a floor by adding it to `FloorPlan.Order`, never by writing a destination somewhere.
- Numbers in code must match the GDD, not the other way round. If an implementation deviates,
  fix the code or the GDD **in the same change** — never leave them out of sync.
- Content lives in `Assets/_Project/Scripts/Content/SeedContent.cs` until it is baked to assets
  (Tools > NO404 > Data > Bake Seed Content To Assets).

## Non-negotiable rules

- No `GameObject.Find`, no `SendMessage`, no string-keyed runtime messaging.
  Wiring goes through `ServiceHub`, `ZoneRegistry` or an explicit reference.
- The event bus carries typed structs only (`Assets/_Project/Scripts/Core/GameEvents.cs`).
  Subscribe in `OnEnable`, unsubscribe in `OnDisable`. Events must never trigger a scene load.
- No user-visible string is hard-coded. Every one comes from `Resources/NO404/strings.csv`
  through `Loc.T(key)`. Never concatenate translated fragments — add one key with `{0}` slots.
- No third-party packages, no DI container, no runtime code generation, no node-graph plugin
  as a core dependency. **One named exception:** `com.unity.netcode.gameobjects`, added for the
  1–4 player co-op v3.0 is built around. It is Unity first-party and v3.0 Phase 0 requires a
  network skeleton before anything else ("멀티를 마지막에 붙이지 않는다"), which a hand-rolled
  transport on top of the vendored Steamworks.NET could also have satisfied — this was a
  deliberate call, not a drift. Adding any further package still needs the same explicit
  decision written down here.
- **Burst AOT is off** (`ProjectSettings/BurstAotSettings_StandaloneWindows64.json`). Netcode
  pulls in Collections, which pulls in Burst, and the Burst step inside the player pipeline
  killed `bee_backend.exe` — every Windows build failed in "Postprocess built player" with
  `Pipe is broken` and produced an `.exe` that ran but never wrote a log line. Nothing in this
  game is a Burst job; only Collections' internals would benefit, and at this message volume
  that is nothing. Turn it back on only with a build that finishes.
- **The two prefabs under `Assets/_Project/Resources/NO404/Net/` are generated, not authored.**
  `ShiftObject.prefab` carries the shift; `NetPlayer.prefab` is one caretaker. They are the
  only prefab assets in the project and they exist because Netcode stamps a NetworkObject's id
  hash at import: an object built with `new GameObject()` cannot be spawned.
  `Tools > NO404 > Net > Rebuild Net Prefabs` recreates them, and the editor does so
  automatically when either is missing. Do not add a third — everything else the shift needs
  travels as data on these two.
- Game time is integer game seconds (`GameClock`). Never accumulate float time.
- Every P0 case needs a fail-safe. A single wrong judgement must never permanently block the
  truth ending — provide alternate evidence instead (GDD 11.2 / 14.4).
- CCTV anomalies stay on screen at least 4 seconds and must be re-checkable (GDD 12.4).
- A visitor can only be judged "correct" when the player consulted at least two independent
  facts (GDD 13.2).
- Saves are written atomically with a checksum, and old schemas are migrated, never discarded.
- No `TODO` left in a file that is claimed to be done. Steam integration points are marked
  `TODO-STEAM` on purpose and are the only exception.

## Workflow

1. Read the relevant GDD sections before writing code.
2. State the file-level plan before implementing.
3. Implement, then run `Tools > NO404 > Data > Validate Content` and the EditMode tests.
4. List every created/modified file and any manual Unity Editor step that is still required.

## Definition of done

- Compiles with no errors and no new warnings.
- EditMode tests pass (`Window > General > Test Runner`).
- Content validation reports zero errors.
- The affected flow is playable start to finish without a soft-lock.
- New user-visible text has both `ko` and `en` entries in the string table.
