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

The `spike/3d-isometric-runtime` branch contains the isolated 3D migration
slice; the stable 2D run loop remains unchanged. Open
`scenes3d/TestArena3D.tscn` for the playable preview, or open
`scenes3d/RunShell3D.tscn` for the run-owned map shell.

The 3D slice now has a fixed tilted Camera3D, WASD movement on the XZ plane,
mouse ground aiming, four skills, Feral, Spitter, Brimstone Colossus, faction
aware damage, drops, equipment, GameOver, MapComplete, reward choice, next-map
transition, and minimal save/restore. `GreyboxStressArena3D.tscn` is the fixed
20–40 enemy pressure map with a narrow path, slope cue, occluder-sized block,
and a runtime NavigationMesh placeholder. Its smoke is intentionally a
structure-and-pressure check; it does not yet claim obstacle pathfinding.

## Run the playable slice

Open the repository with Godot 4.7.1 .NET and run the main scene. On
`migration/3d-mainline` it loads `scenes3d/RunShell3D.tscn`, which owns the 3D
run session and transitions between map instances. The 2D runtime under
`scenes/` is retained as a legacy/reference implementation; its Domain rules
and key regression smokes remain part of CI.

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
`Milestone20ContentRuntimeSmoke.tscn`. The 3D contract smokes are
`Isometric3DSpike.tscn`, `Spitter3DRegressionSmoke.tscn`,
`Brimstone3DRegressionSmoke.tscn`, `RunLoop3DRegressionSmoke.tscn`,
`GameOver3DRegressionSmoke.tscn`, and
`GreyboxStress3DRegressionSmoke.tscn`, plus
`SaveRecovery3DRegressionSmoke.tscn` for atomic rollback and post-death
Playing-state restoration. The stabilization branch runs these alongside the
legacy 2D smokes from `.github/workflows/ci.yml`.

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
