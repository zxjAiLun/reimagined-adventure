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

`main` is the stable milestone baseline and `dev` is the active development
branch. The 2D runtime under `scenes/` is retained only as a legacy behavioral
reference. New product features target
`scenes3d/` and `scripts3d/`. The product entry scene is
`scenes3d/GameBootstrap3D.tscn`; open
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
Stage 5/6 run progression now keeps Map Level growth in the run session and
resolves one deterministic modifier per map from
`resources/RunMapModifierCatalog3D.tres`. The initial 3D catalog contains
Quiet Coast, Hardened Front, and Volatile Hunt. Modifier selection derives a
separate seed from Run Seed, Map Level, and catalog version, so it does not
advance loot, crafting, or event RNG. Encounter spawn contexts consume the
resolved effects before enemy `_Ready`, while the HUD presents the current
modifier and its risk/reward text through run-session signals. Save/restore and
same-map retries do not reroll the modifier.
Stage 7 adds a deterministic encounter plan on top of the modifier plan.
`resources/RunEncounterCatalog3D.tres` selects a validated, tiered encounter
definition using an isolated encounter RNG namespace derived from the run seed,
map level, and catalog version. The selected plan is applied to `TestArena3D`
before its scene-tree ready phase, so every enemy receives the same encounter,
wave, modifier, and seed context. The HUD presents the encounter, tier, wave,
and active-enemy count, and the plan is cached across save/restore and map
transitions without rerolls.
Stage 8 adds a run-owned Atlas route layer. `resources/RunAtlas3D.tres`
contains the fixed Quiet Coast, Hardened Frontier, Volatile Rift, Brimstone
Caldera, and Siege Gate routes. Completing a map through the real
`EncounterCompleted` signal unlocks its valid next routes; an isolated Boss
death does not complete the Atlas. After choosing the reward, keys `1`/`2`/`3`
select a route and `N` confirms it. The route panel presents tier, modifier,
encounter, description, and item level, while the selected route is persisted
across Playing, MapComplete, reward, and pending-route save boundaries.
The Stage 8 smokes execute Quiet/Crossfire/Siege through the real encounter
director and verify one-shot wave/completion signals, old-map release,
route-driven map plans, and deterministic Atlas save recovery.

The productization round starts with a formal main menu. New Run creates a
clean map-one session and removes the previous run save; Continue is enabled
only for a structurally valid save. Startup restore configures RunSession,
Atlas, modifier, and encounter identity before the target map enters `_Ready`,
then reuses the existing atomic SaveBoundary to restore player, build, reward,
and flow state. A failed content-level restore returns to the menu without
leaving a partially applied run.
Stage 20 adds an Escape pause menu with complete control help, persisted master
volume and fullscreen settings, and save-before-return to the main menu. Stage
21 adds map-local impact presentation driven only by resolved `DamageResult`:
an AudioStreamGenerator placeholder hit sound, bounded CameraRig shake, and a
short real-clock hit-stop that always restores `Engine.TimeScale` on timeout or
map release. Dynamic enemies register through the same map-scoped feedback
source lifecycle as damage numbers.
Stage 22 promotes the production RunShell to the inherited
`QuietCoastArena3D.tscn` presentation scene. It keeps the validated TestArena
combat and baked navigation contract while adding a coastal sky/fog palette,
path language, lights, objective beacon, replaceable environment dressing,
map-intro guidance, and a dedicated non-navigation occlusion layer that fades
foreground structures between the Camera3D and player. The HUD panels were
also resized so modifier, encounter, progression, ailment, and skill text no
longer overlap at the default window size.

The Character Mastery & Combat Depth round keeps character progression owned by
the run: `TotalExperience` and `AllocatedPassiveNodeIds` are persisted beside
the existing equipment, support, stash, reward, and Atlas state. Temporary
combat state is intentionally not a mid-frame save contract: active Burning,
Chilled, and Shocked effects, Elite current health, Boss phase, live wave
enemies, and Boss hazards are discarded on restore. A Playing restore rebuilds
the current map and encounter with clean transient combat state, then reapplies
the saved Run/Build state atomically.

## Run the playable slice

Open the repository with Godot 4.7.1 .NET and run the main scene. The
3D product line loads `scenes3d/GameBootstrap3D.tscn`; choose New Run or
Continue, then the instantiated `RunShell3D` owns the run session and
transitions between map instances. The 2D runtime under
`scenes/` is retained as a legacy/reference implementation; its Domain rules
and key regression smokes remain part of CI.

The checked-in `Windows Desktop` export preset produces the playtest build at
`build/windows/ReimaginedAdventure.exe` (the ignored `build/` directory is a
local artifact):

```powershell
godot --headless --path . --export-release "Windows Desktop" "build/windows/ReimaginedAdventure.exe"
```

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
| I | Open/close the build inventory during Map Complete |
| X | Unequip the first occupied slot during Build Management |
| T | Move the selected item between Inventory and Stash |
| C | Reforge the selected item |
| O / P | Attach / detach the Primary Volley support |
| V | Toggle the Passive panel during Build Management |
| Up / Down | Select an item or passive node |
| G | Allocate the selected passive node |
| B | Complete Build Management and open route choice |
| R | Restart after Game Over / Map Complete |
| Esc | Pause/resume during Playing; view controls and settings |

The fixed arena contains a deterministic map modifier, a Loot Cache, Feral,
Spitter, Brimstone Colossus, and the three map rewards.
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
`SaveValidationTests`, plus `EncounterSelectionTests`) instead of one
monolithic test file.

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
`EnemyScaling3DRegressionSmoke.tscn` also runs real Feral, Spitter, and Boss
damage paths, validates actual drop item levels, and confirms fresh map-one
enemy instances retain their base resources after map-four scaling.
`MapModifierSelection3DRegressionSmoke.tscn` verifies deterministic
selection, level filtering, invalid catalogs, and untouched RNG streams;
`MapModifierRuntime3DRegressionSmoke.tscn` verifies all three shipped
modifiers through real enemy contexts, damage results, drops, and cross-map
resolution. `EncounterSelection3DRegressionSmoke.tscn` verifies deterministic
tier/range selection, isolated encounter seeds, invalid catalogs, and untouched
RNG streams. `EncounterPlanRuntime3DRegressionSmoke.tscn` verifies pre-ready
plan application, cross-map identity, HUD/director binding, save stability,
pause stability, and the shipped Quiet/Crossfire/Siege compositions.
`AtlasRouteChoice3DRegressionSmoke.tscn` verifies real encounter completion,
GameOver completion guards, Map 1 → Map 2 → Map 3 → Map 4 route progression,
Tier 3 eligibility filtering, route-driven map planning, and old-map release;
`AtlasRouteSaveRecovery3DRegressionSmoke.tscn` verifies Playing, MapComplete
before/after reward, pending-route, and next-map save boundaries without
rerolling Atlas state or run RNG.
`BuildIntermission3DRegressionSmoke.tscn` drives the formal build input path
for equip/unequip, bidirectional stash transfer, reforge, support changes, UI
labels, and build completion; `MalformedSave3DRegressionSmoke.tscn` verifies
file-level null-collection and malformed-JSON rejection without live-state
mutation; `MapScope3DRegressionSmoke.tscn` verifies overlapping-map drop
ownership and local build-flow access. The mastery round adds
`CharacterProgression3DRegressionSmoke.tscn` and
`PassiveTree3DRegressionSmoke.tscn` for run-owned XP, stable passive IDs,
legacy migration, build allocation, and save recovery;
`AilmentRuntime3DRegressionSmoke.tscn` and
`AilmentLifecycle3DRegressionSmoke.tscn` for Burning, Chilled, Shocked,
pause/death cleanup, and isolated ailment RNG; and
`EliteSelection3DRegressionSmoke.tscn` and
`EliteRuntime3DRegressionSmoke.tscn` for deterministic modifier selection,
pre-ready elite scaling, rewards, and combat presentation.
`BossPhase3DRegressionSmoke.tscn` covers Brimstone phase thresholds, the
phase-two add wave, Molten Ring, Ember Barrage, deterministic Lava Eruption,
pause cancellation, death cleanup, and Map Complete. The end-to-end
`MasteryCombatDepth3DRegressionSmoke.tscn` ties support loadout, progression,
passive damage, ailments, and save restore together.
`RunEntry3DRegressionSmoke.tscn` verifies absent and malformed save handling,
New Run, plan-correct Map 4 continuation, and a clean restart.
`PauseSettings3DRegressionSmoke.tscn` verifies pause freeze, resume, control
help, persisted master volume/fullscreen settings, save-and-return, Continue,
and terminal-state guarding. `CombatImpactFeedback3DRegressionSmoke.tscn`
verifies zero filtering, light/heavy impacts, generated audio, camera shake,
real-clock hit-stop, dynamic source registration, pause freeze, and map-exit
time-scale cleanup. `QuietCoastPresentation3DRegressionSmoke.tscn` verifies the
formal inherited map, environment/dressing/objective structure, HUD bounds,
pause-safe intro, camera occlusion fade/restore, and unchanged baked navigation.
`ProductPerformance3DRegressionSmoke.tscn` runs the formal map with 40 live
navigation actors, bounds frame and crowd-coordinator cost, and requires real
movement progress. The local Stage 23 baseline measured 3.70 ms average frame
work, 10.44 ms maximum frame work, and 0.20 ms average crowd coordination across
all 780 pairs. CI runs 55 smoke scenes in total and validates that the Windows
export preset can produce a resource pack.

## Migration boundaries

Gameplay rules and portable content data live in `src/Arpg.Domain`. Godot
scenes, Nodes, resources, UI, InputMap, telegraphs, and particles live in the
root project. Deferred large systems such as procedural maps, encounter
composition beyond the fixed Stage 4 waves, the full Boss catalogue, and
advanced exceptions are outside the current product slice.

The `isometric-3d-parity-v1` tag marks the completed 3D parity stabilization
boundary. Further work is organized as reviewed product stages on `dev` and
is promoted to `main` only after a complete product milestone is accepted.
