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

## 3D product runtime

`main` is the active product baseline and `product/encounter-runtime` is the
current Stage 4 development branch. The 2D runtime under `scenes/` is
retained only as a legacy behavioral reference. New product features target
`scenes3d/` and `scripts3d/`. Open
`scenes3d/TestArena3D.tscn` for the playable preview, or open
`scenes3d/RunShell3D.tscn` for the run-owned map shell.

The 3D slice now has a fixed tilted Camera3D, WASD movement on the XZ plane,
mouse ground aiming, four skills, Feral, Spitter, Brimstone Colossus, faction
aware damage, drops, equipment, GameOver, MapComplete, reward choice, next-map
transition, and minimal save/restore. `GreyboxStressArena3D.tscn` is the fixed
20–40 enemy pressure map with a narrow path, slope cue, occluder-sized block,
and a runtime NavigationMesh placeholder. Its smoke remains intentionally a
structure-and-pressure check; it does not claim obstacle pathfinding.
The dedicated `NavigationFoundationArena3D` is the Stage 3A planar navigation
fixture. It uses a repository-baked `NavigationMesh` resource generated from
the arena floor and static obstacle geometry, with a non-zero agent radius.
`NavigationFoundation3DRegressionSmoke` verifies a turning path around the
obstacle, Feral chasing, pause/resume, the unchanged Windup/Impact contract,
and a dynamically instantiated Feral. Stage 3B also routes Spitter
approach/retreat through the same adapter, keeping it inside a preferred
distance band while preserving its locked Aim/Windup telegraph direction.
Stage 3C adds a separate `CrowdNavigationStressArena3D.tscn` with a saved
offline navigation resource, 24-agent default pressure (16 Feral and 8
Spitter), deterministic symmetric separation, congestion recovery, and
registration cleanup when dynamically spawned enemies leave the tree. Its
`CrowdNavigation3DRegressionSmoke` expands the same arena to 40 agents and
keeps the pair pass bounded at `n(n-1)/2`; the legacy `GreyboxStressArena3D`
is unchanged. Stage 3D adds `BossNavigationStressArena3D.tscn` with two saved
offline navigation layers: small agents use a 0.55 m radius mesh that includes
the 1.8 m central choke, while the 1.2 m radius Boss mesh excludes that choke
and uses the 3.2 m-plus end routes. `BossNavigation3DRegressionSmoke` verifies
large-agent routing, Feral access to the narrow route, crowd-safe locked Slam
and Spear attacks, pause/resume, and immediate Boss death cleanup.
`NavigationPressureBaseline3DRegressionSmoke` uses a pressure-only wall-safe
Feral layout and requires actual progress from at least 20/24 and 32/40 small
agents; path ownership is diagnostic only. Its 40-agent-plus-Boss stage checks
small-agent progress separately from Boss displacement/distance progress and
also verifies that GameOver freezes navigation metrics. The shared Boss
controller has no direct-chase fallback: missing, unsynchronized, unreachable,
or pathless navigation stops the Boss. `TestArena3D` uses the saved
`resources/TestArenaNavigation.tres` for both small and large layers, and its
Boss smoke includes the unavailable-layer stop/recovery path. The pressure smoke
prints coordinator physics timing for 24, 40, and 40-plus-Boss samples. Both
meshes are repository resources; this stage does not rebake navigation at
runtime and does not enable RVO avoidance.
Navigation is currently planar XZ: actors do not jump, traverse multilevel
terrain, or trigger runtime rebakes.
Combat hits now also publish authoritative DamageResult feedback: positive hits
create world-space damage numbers, trigger isolated-material hit flashes, and
run immediate collision/physics cleanup plus a short death scale presentation.
The Stage 4 encounter runtime is data-driven: `DefaultEncounter3D.tres` owns
three waves (4 Feral, 3 Feral + 2 Spitter, then 1 Brimstone Colossus), while
`EncounterDirector3D` owns timing, spawn-point selection, active counts, and
the single `EncounterCompleted` signal consumed by `GameFlowController3D`.
Production `RunShell3D` maps no longer contain static enemy nodes; old direct
`TestArena3D` contract smokes create isolated legacy fixtures only.

## Run the playable slice

Open the repository with Godot 4.7.1 .NET and run the main scene. On
the 3D product line loads `scenes3d/RunShell3D.tscn`, which owns the 3D
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
Playing-state restoration, plus `CombatHud3DRegressionSmoke.tscn` for the
signal-driven combat HUD contract, and
`CombatTelegraphs3DRegressionSmoke.tscn` for enemy windup, impact, locked
direction, and pause safety, and `CombatHitFeedback3DRegressionSmoke.tscn` for
damage numbers, zero-damage filtering, multi-projectile hits, pause freezing,
hit flash, death cleanup, loot, and Boss Map Complete, and
`NavigationFoundation3DRegressionSmoke.tscn` for baked planar obstacle routing,
pause safety, Feral Windup, and dynamic Feral registration. CI runs these
alongside `SpitterNavigation3DRegressionSmoke.tscn`,
`CrowdNavigation3DRegressionSmoke.tscn` for 24/40-agent separation,
congestion, pause, and unregister contracts, and the legacy 2D smokes from
`.github/workflows/ci.yml`. `CrowdNavigationMapLifecycle3DRegressionSmoke.tscn`
also exercises the old-map QueueFree/new-map overlap window and verifies that
the new map keeps its own coordinator binding. `BossNavigation3DRegressionSmoke.tscn`
and `NavigationPressureBaseline3DRegressionSmoke.tscn` add the Stage 3D
large-agent route, locked-attack, pressure-bound, and pause-freeze contracts.
`EncounterDirector3DRegressionSmoke.tscn` verifies wave composition, dynamic
spawn counts, one-shot completion, and Map Complete.
`EncounterLifecycle3DRegressionSmoke.tscn` verifies paused spawning is frozen
and a next map receives a fresh director instance.

## Migration boundaries

Gameplay rules and portable content data live in `src/Arpg.Domain`. Godot
scenes, Nodes, resources, UI, InputMap, telegraphs, and particles live in the
root project. Deferred large systems such as procedural maps, encounter
composition beyond the fixed Stage 4 waves, the full Boss catalogue, and
advanced exceptions are outside the current product slice.

The `isometric-3d-parity-v1` tag marks the completed 3D parity stabilization
boundary. Further work is organized as reviewed product stages; the next
stage is not started until the current stage is accepted.
