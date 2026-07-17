# Publishing to Paradox Mods (the workflow that actually works)

Everything runs headless from a terminal — no Visual Studio involved. This file is
the authoritative workflow, verified end-to-end across the sibling mods (Building
Reviver's first publish and Cemetery Overflow Manager's first publish both ran
through it).

## One-time machine setup

1. Install CS2, then the modding toolchain: in-game **Options → Modding** →
   download/install all toolsets. This sets the `CSII_TOOLPATH` env var — restart
   the terminal afterwards.
2. Install a .NET SDK (8+ works; the toolchain's tools target .NET 6, hence the
   roll-forward below).
3. The first publish on a machine asks for a Paradox login once, interactively.
   After that, ModPublisher auto-logs-in with the cached session. Never put
   credentials in a file (`--noAutoLogin`/`-e`/`-p`/`PDXAccountDataPath` exist but
   plaintext credentials are strictly worse than the cached session).

## Every publish

- **Exit CS2 completely first** — the game holds the mod DLL open and the build
  fails with "Access to the path ... denied".
- Thumbnail must exist at `Properties/Thumbnail.png`: square PNG, ≥256×256
  (512×512 looks better). No spaces in any image filename or the upload fails
  with "Couldn't upload all files to the backend".

From the project folder, in PowerShell:

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'

# First publish (ModId must be empty in PublishConfiguration.xml):
dotnet publish -c Release /p:ModPublisherCommand=Publish
#  -> prints "Mod published with Id=NNNNNN"; copy that into <ModId> and commit it.

# New version (binary changed) - bump <ModVersion> and write <ChangeLog> first:
dotnet publish -c Release /p:ModPublisherCommand=NewVersion

# Listing-only change (description, images, links; no rebuild, no version bump):
dotnet publish -c Release /p:ModPublisherCommand=Update
```

## After publishing

- Verify the listing at `https://mods.paradoxplaza.com/mods/<ModId>/Windows`
  (new mods take a little while to appear in search).
- **Delete the local copy** from
  `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\<ModName>\`
  and subscribe in-game instead — a local build silently shadows the subscribed
  mod, so you'd never be testing what players get. Every `build.ps1` run
  re-creates the local copy; delete it again when done testing.
- Forum thread: create it in the **Cities Skylines 2: User Mods** sub-forum
  (https://forum.paradoxplaza.com/forum/forums/cities-skylines-2-user-mods.1170/),
  not the parent CS2 forum, and put its URL in `<ForumLink>` via a listing-only
  Update. The thread doubles as the mod page's discussion feed.

## Mod IDs on this account (lukyguy117)

- Auto Bulldozer [Beta]: **151415**
- Building Reviver: **151484**
- Cemetery Overflow Manager: **151615**
- Prison Overflow Manager [Beta]: **151632**
