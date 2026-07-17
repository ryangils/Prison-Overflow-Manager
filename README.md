# Prison Overflow Manager — Cities: Skylines II mod

Eases prison-transport backlogs. When sentenced criminals pile up at police stations waiting for a prison van that never comes, every prison temporarily gets more van capacity so more vans are dispatched to collect them; once the backlog clears, van capacity goes back to vanilla. Toggleable in Options → Mods → Prison Overflow Manager, with a master switch, a backlog threshold, an adjustable scale factor, an opt-in last-resort early release, and session statistics (with a reset button).

Addresses the long-standing "prisoners stuck at police stations" bug (they serve their sentence in the holding cell because no prison van is dispatched) without removing the gameplay loop: you still build prisons, vans still drive, criminals still get collected — your prisons just run more vans during a crime wave. The fourth mod in the set alongside [Auto Bulldozer](https://github.com/ryangils/AutoBulldozer), [Building Reviver](https://github.com/ryangils/Building-Reviver), and [Cemetery Overflow Manager](https://github.com/ryangils/CemeteryOverflowManager).

## Prerequisites (one-time)

1. Windows with Cities: Skylines II installed.
2. Visual Studio 2022 (free Community edition works) with the ".NET desktop development" workload.
3. Install the CS2 modding toolchain: launch the game, go to **Options → Modding** (or via the Paradox launcher's game settings) and download/install all modding toolsets. This sets the `CSII_TOOLPATH` environment variable that this project's `.csproj` relies on. Restart Visual Studio afterwards so it picks up the variable.

## Build

Either run `.\build.ps1` from the project folder (needs only the .NET SDK — no Visual Studio; the script sets `DOTNET_ROLL_FORWARD=LatestMajor` because the toolchain's post-processor targets .NET 6), or:

1. Open `PrisonOverflowManager.sln` in Visual Studio.
2. Set configuration to **Release** and build (Ctrl+Shift+B).

Either way, the toolchain automatically copies the built mod to `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\PrisonOverflowManager\`.

If the build fails with missing references or import errors, `CSII_TOOLPATH` isn't set — redo step 3 of the prerequisites.

## Test in-game

1. Launch CS2 and load a city (or start one).
2. Check **Options → Mods → Prison Overflow Manager** — you should see the toggles.
3. Let the simulation run and watch the "Criminals awaiting a prison van" readout. Trigger a backlog (a crime wave, or a city whose prisons are undersized or far away) and confirm "Scaling currently active" flips to Yes once the backlog crosses the threshold, then back to No once it clears. The log is at `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\PrisonOverflowManager.log`.

## Publish to Paradox Mods (PDX Mods)

See [PUBLISHING.md](PUBLISHING.md) — publishing is a single `dotnet publish` command per action (first publish / new version / listing-only update), run from a terminal with the game closed. A square PNG thumbnail at `Properties/Thumbnail.png` is required, and after the first publish you copy the printed Mod ID into `PublishConfiguration.xml` yourself.

## How it works

`PrisonOverflowSystem` is an ECS system that runs during the simulation phase (1–64 sweeps per in-game day, configurable, default 16; no cost while paused or in menus).

**The backlog signal is city-wide, on the citizen side.** A criminal gains the `Sentenced` flag while still held at a police station, and gains the `Prisoner` flag the moment a prison van actually collects them. So the mod counts criminals that are `Sentenced` but not yet `Prisoner` — exactly the population stuck waiting for a van. This is the direct analog of Cemetery Overflow Manager's "dead and awaiting a hearse" count, and it means one full prison next to an idle one doesn't trigger scaling; only a genuine city-wide shortfall does.

**Soft scaling (default ON).** Prison van capacity lives on the prison *prefab* (`PrisonData.m_PrisonVanCapacity`), not on each placed building, so scaling the prefab raises van capacity for every prison of that type at once. The game's `PrisonAISystem` reads this value fresh every tick and feeds it to `BuildingUtils.GetVehicleCapacity`, so more vans are dispatched to collect prisoners from police stations. Capacity is always computed from each prefab's true authoring baseline (`Game.Prefabs.Prison`, read via `PrefabSystem` — the runtime `Game.Buildings.Prison` is a distinct same-named component and is deliberately not the one read), never from the live value, so repeated sweeps never compound. Once the backlog falls to half the threshold, vanilla van capacity is restored.

**Only van capacity is scaled — not prisoner holding capacity.** The documented bug is purely about vans not being dispatched, so that's the only lever this mod touches. Leaving prison holding capacity (default 500) alone means the "build more prisons when yours are full" loop stays intact, and it sidesteps any risk from shrinking holding capacity back down while a prison is over the restored limit. When van capacity is restored mid-operation, the vanilla tick removes any excess parked vans itself, so restoring is always safe.

**Early release (default OFF, opt-in).** A last resort for saves where prisoners genuinely never get collected — for example a city with no prison at all, where sentenced criminals accumulate in police-station holding cells forever and eventually stop the station from arresting anyone new. When enabled, criminals who have been awaiting a van for longer than the grace period (1–30 in-game days) are freed the same way the game frees them when a sentence ends: their `Criminal` component is removed, along with any lingering jail-related travel purpose, and the police station drops them from its cells on its next tick. Nobody is deleted — they simply stop being criminals and go free. Grace tracking is dip-tolerant: because soft scaling exists precisely to push the backlog under the threshold, a brief dip below it holds the accumulated grace periods rather than wiping them, and tracking only resets after a full in-game day genuinely under the threshold.

Because it only reads/writes vanilla components, it's save-safe and can be added or removed from a save at any time. Switching the mod off restores vanilla van capacity immediately.

## Files

- `Mod.cs` — entry point (`IMod`): loads settings, registers options UI and locale, schedules the system
- `Setting.cs` — options UI definition and persisted settings
- `LocaleEN.cs` — English strings for the options UI
- `PrisonOverflowSystem.cs` — the backlog measurement, van scaling, and early-release logic
- `Properties/PublishConfiguration.xml` — PDX Mods listing metadata
