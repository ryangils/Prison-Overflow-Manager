# Prison Overflow Manager — Cities: Skylines II mod

Fourth mod in the set alongside sibling projects `../AutoBulldozer`, `../BuildingReviver`, and
`../CemeteryOverflowManager` (read them for the established code style — same author, same
conventions, same toolchain). This mod is a near-direct port of Cemetery Overflow Manager's
architecture to the prison-transport domain.

**Status (2026-07-17 evening): PUBLISHED — ModId 151632, v1.0.0, account lukyguy117, display name
"Prison Overflow Manager [Beta]".** Soft scaling verified in-game on a real city before release
(first test session below); the shipped build includes the restore-counter fix (rebuilt after the
tested session — the fix is cosmetic, counters only). **Early release shipped UNVERIFIED** (off by
default; the release path, the dip-tolerance guard, and the recovery reset have never executed
in-game — same posture the cemetery mod shipped with, see its CLAUDE.md for the precedent).

## Goal

Sentenced criminals pile up in police-station holding cells when no prison van is dispatched to
collect them — a documented vanilla bug (prisoners "serve their sentence at the police station").
The only existing mitigation on Paradox Mods is StarQ's Prefab Asset Fixes, which statically bumps
the prison van count 10→20. This mod does that adaptively: scale van capacity up during a backlog,
restore it after — same "keep the loop, just work harder during a spike" philosophy as the cemetery
mod, deliberately less blunt than a "remove crime" mod.

## Decided design (and why it differs from the cemetery mod)

**1. Backlog signal — citizen-side, `Sentenced && !Prisoner`.** `Game.Citizens.Criminal.m_Flags`
gains `Sentenced` while the criminal is still `Arrested` at a police station, and gains `Prisoner`
the instant a van collects them (`CriminalSystem` → `GoToPrison`). So `Sentenced && !Prisoner` is
exactly "awaiting a prison van" — the direct analog of the cemetery mod's `Dead && RequireTransport`.
Query `All<Criminal>, None<Deleted, Temp>`, flag-check per entity.

**2. Soft scaling (default ON) — van capacity ONLY.** Lever is `Game.Prefabs.PrisonData.
m_PrisonVanCapacity` (int, runtime, writable). `PrisonAISystem.PrisonTickJob.Tick` reads it fresh
from the prefab lookup every tick (+ `UpgradeUtils.CombineStats` for upgrade prefabs, which carry
their own `PrisonData`) and feeds it to `BuildingUtils.GetVehicleCapacity` — never cached at spawn,
so baseline-anchored scaling works exactly like deathcare. **Unlike the cemetery mod, which scaled
three fields, this scales only `m_PrisonVanCapacity`:** the bug is purely van dispatch; leaving
`m_PrisonerCapacity` (holding, default 500) alone keeps the "build more prisons" loop and dodges the
restore risk of shrinking holding capacity below the live occupant count. Restoring van capacity
mid-op is safe — `PrisonTickJob` deletes excess parked vans itself when capacity shrinks
(decompiled: `while (parkedVehicles.Length > max(0, m_PrisonVanCapacity + availableVehicles -
vehicleCapacity))`).

**3. Early release (default OFF) — the "hard cleanup" analog, but gentler.** Instead of deleting
citizens (cemetery mod deletes uncollected bodies), this **frees** stuck criminals: mirror
`CriminalSystem`'s own sentence-end path — `RemoveComponent<Criminal>` plus remove `TravelPurpose`
if its `Purpose` is jail-related (`GoingToJail/InJail/GoingToPrison/InPrison`). The police station
self-heals its `Occupant` buffer (drops occupants lacking a `Criminal` component, confirmed in both
`PoliceStationAISystem.Tick` and `CriminalSystem`). Nobody dies — they just stop being criminals.
Same dip-tolerant grace tracking as the cemetery mod (`m_LastBacklogFrame` + `kRecoveryFrames`,
frame-based so the sweeps-per-day slider can't change cleanup semantics).

## Baseline / prefab gotcha (identical to the cemetery mod)

`Game.Prefabs.Prison` (authoring `ComponentBase`, `m_PrisonVanCapacity = 10`, `m_PrisonerCapacity =
500`) and `Game.Buildings.Prison` (runtime `IComponentData`) are two distinct types with the same
short name. `TryGetBaseline` fully-qualifies `Game.Prefabs.Prison` — binding to the runtime one
would silently never match. Read via `PrefabSystem.TryGetPrefab<PrefabBase>` +
`PrefabBase.TryGetExactly<Game.Prefabs.Prison>`.

## Verification method (how the ECS facts above were established)

Decompiled `Game.dll` v1.6.0 directly with `ilspycmd` (needs `DOTNET_ROLL_FORWARD=LatestMajor` —
ilspycmd targets .NET 6, machine has .NET 8). Path from `CSII_MANAGEDPATH`. Dumped type lists with
`ilspycmd <dll> -l c|s|e` and individual types with `ilspycmd <dll> -t <FullTypeName>`. All types,
fields, flags, and the AI-tick read paths cited above were read from the actual assembly, not
assumed. Full findings mirrored to the user's memory as `cs2-prison-ecs-findings`.

## Project conventions mirrored from siblings

- License MIT, copyright `ryangils 2026` (copied from `../CemeteryOverflowManager/LICENSE`).
- `Mod.cs`: `IMod`, static `ILog Log` + static `Setting`, system at `SystemUpdatePhase.GameSimulation`.
- `Setting.cs`: `ModSetting`, `[FileLocation("ModsSettings/PrisonOverflowManager/...")]`, groups
  Options / Van scaling / Early release / Timing / Statistics; sweep-frequency slider; reset button
  with `[SettingsUIConfirmation]`.
- `PrisonOverflowSystem.cs`: `GameSystemBase`, same `kFramesPerDay` / `kMaxSweepsPerDay` / throttled
  `OnUpdate` / baseline-anchored scaling / dip-tolerant grace tracking as
  `../CemeteryOverflowManager/OverflowManagerSystem.cs`.
- `build.ps1`, `.gitignore`, `PublishConfiguration.xml`, `PUBLISHING.md`: copied from a sibling and
  renamed. Publishing account is lukyguy117.

## Defaults

Enable ON, soft scaling ON, backlog threshold 10 (prison backlogs run smaller than death waves —
cemetery defaulted to 20), scale factor 200% (van 10→20, matching Prefab Asset Fixes), early release
OFF, grace 7 days, 16 sweeps/day.

## Test results (2026-07-17, first in-game session, default settings, real established city)

```
15:47:02 OnLoad
15:47:02 Loaded from .../Mods/PrisonOverflowManager/PrisonOverflowManager.dll
15:49:58 Backlog reached 10 criminal(s) awaiting a prison van; scaled 1 prison prefab(s) to 200% of vanilla van capacity.
15:58:30 Backlog cleared; restored 3 prison prefab(s) to vanilla van capacity.
```

What this proves:

- **The citizen query is correct and the niche is real.** The user's existing city hit the default
  threshold (10 stuck criminals) within ~3 minutes of loading, with no provocation whatsoever.
- **The prefab write path works and the sim honours it.** Backlog drained from 10 to ≤5 in ~8.5
  minutes of scaled van capacity — vans were actually dispatched and collected prisoners.
- **No compounding.** Zero `Re-applied prison van scaling...` lines across ~8 minutes of active
  sweeps — every re-sweep computed a value identical to what was already written, so the
  authoring-baseline anchoring is provably correct (the key cemetery-mod lesson, reconfirmed).
- **No flapping, no interference.** Exactly one scale-up and one restore; no `(N left alone...)`
  suffix, so no other mod touched `PrisonData` mid-cycle. No mod-related exceptions in Player.log.

**Quirk found and fixed in source (NOT in the tested build): "scaled 1" vs "restored 3".** Vanilla
has 3 prefabs with authoring `Game.Prefabs.Prison` (base Prison + 2 upgrades); only the base has
nonzero van capacity, so only it actually changes on apply — but `RestoreScaling` counted no-op
writes. Fixed by skipping no-op restores. Symmetric counters after next rebuild.

Still unexercised: early release (both the release path and the dip-tolerance guard), the
`skipped`/"left alone" path, and behaviour across a save load while scaling is active.

## What still needs doing

1. **In-game test (not started).** Load a city, enable the mod, confirm the options UI shows and the
   "Criminals awaiting a prison van" readout tracks reality. Provoke a backlog (crime wave, or
   undersized/distant prisons) and confirm "Scaling currently active" → Yes at threshold, → No once
   drained to half-threshold. Log at `.../Logs/PrisonOverflowManager.log`.
   - **No-compounding check** (the key cemetery lesson): while scaling stays active over many
     sweeps, there should be ZERO `Re-applied prison van scaling...` lines — every re-sweep must
     compute a value identical to what's already written. If those lines appear, baseline anchoring
     is broken.
   - **Restore check**: exactly one scale-up and one restore per backlog; restore line should have
     no `(N left alone...)` suffix on a clean single-mod save.
2. **Early-release test (off by default, deletes... well, frees citizens — use a throwaway save).**
   Recipe: a city with sentenced criminals but no functioning prison, threshold low, grace 1 day,
   early release on. Confirm the `Early release freed N criminal(s)...` line fires after ~1 in-game
   day, the freed citizens leave the police-station cells, and the game stays stable. Also verify
   the dip-tolerance guard by letting the backlog oscillate across the threshold (this is the branch
   that shipped UNVERIFIED in the cemetery mod — worth actually exercising here).
3. **Thumbnail** (`Properties/Thumbnail.png`, square ≥256×256) before publishing.
4. **Publish** via `PUBLISHING.md`, then record the ModId there and in the user's memory.
