# Reimagined Adventure — Godot migration

This repository contains the Godot 4 .NET migration of the ARPG prototype.
The old C++ project remains a read-only behavior reference; no Godot runtime
code depends on it.

## Toolchain

- Godot 4.7.1 .NET x86_64
- Forward+ renderer for desktop
- .NET SDK 10.0.302 (`global.json`)

The Godot project is the root project. `reimagined adventure.csproj` references
`src/Arpg.Domain/Arpg.Domain.csproj`; the Domain project does not reference
Godot or GodotSharp.

## 3D isometric spike

The `spike/3d-isometric-runtime` branch contains an isolated 3D experiment;
the stable 2D run loop remains unchanged. Open `scenes3d/TestArena3D.tscn` for
the playable preview or run `scenes3d/Isometric3DSpike.tscn` for the automated
ground-aim, Pulse, Dash, drop, pickup, and equipment-damage smoke.

The preview uses a fixed tilted Camera3D, WASD movement on the XZ plane, mouse
ground aiming, one Feral enemy, and the minimal 3D equipment loop. Spitter,
Brimstone Colossus, Atlas, save data, and legacy UI are intentionally outside
this spike.

## Run the playable slice

Open the repository with Godot 4.7.1 .NET and run the main scene. It loads
`scenes/RunShell.tscn`, which owns the run session and transitions between map
instances; `scenes/TestArena.tscn` remains useful as a single-map sandbox.

| Input | Action |
| --- | --- |
| W / A / S / D | Move |
| Mouse cursor | Aim |
| Left mouse button | Spread Shot |
| Right mouse button | Meteor |
| Q | Pulse |
| Space | Dash |
| F | Pick up the nearest drop |
| E | Equip the newest weapon |
| R | Restart after Game Over / Map Complete |

The fixed arena contains a Hardened Front map modifier, a Loot Cache, Feral,
Spitter, Brimstone Colossus, Atlas progression, and the three map rewards.
Boss death enters Map Complete and opens the reward choice; F is handled by one
interaction controller, with map events taking priority over item drops. After
choosing a reward, press N to enter the next map while keeping the run state.

## Tests and smoke scenes

Run the pure rules suite with:

```powershell
dotnet test tests\Arpg.Domain.Tests\Arpg.Domain.Tests.csproj --no-restore -c Release
```

The Domain suite is split by system (`CombatMathTests`, `LootGeneratorTests`,
`EquipmentTests`, `SkillSupportTests`, `MapScalingTests`, and
`SaveValidationTests`) instead of one monolithic test file.

Godot smoke scenes are named `Milestone4Smoke.tscn` through
`Milestone20ContentRuntimeSmoke.tscn`, with the isolated 3D smoke at
`scenes3d/Isometric3DSpike.tscn`. They cover enemy behavior, equipment,
flow states, map events, supports, passive allocation, content save/restore,
map modifiers, Atlas progression, Stash, and Crafting. The stabilization
branch also runs the main scene, combat smoke, save smoke, and content smoke
from `.github/workflows/ci.yml`.

## Migration boundaries

Gameplay rules and portable content data live in `src/Arpg.Domain`. Godot
scenes, Nodes, resources, UI, InputMap, telegraphs, and particles live in the
root project. Deferred large systems such as procedural maps, encounter
composition, the full Boss catalogue, and advanced exceptions are outside the
first vertical slice.

The `pre-stabilization` tag marks the last baseline before run-loop work.
Development after that point is on `stabilization/run-loop-v1`; no new
Milestone 21+ content is being added until this run-loop is stable and CI is
green.
