# Reimagined Adventure — Godot migration

This repository contains the Godot 4 .NET migration of the ARPG prototype.
The old C++ project remains a read-only behavior reference; no Godot runtime
code depends on it.

## Toolchain

- Godot 4.7.1 .NET x86_64
- Compatibility renderer
- .NET SDK 10.0.302 (`global.json`)

The Godot project is the root project. `reimagined adventure.csproj` references
`src/Arpg.Domain/Arpg.Domain.csproj`; the Domain project does not reference
Godot or GodotSharp.

## Run the playable slice

Open the repository with Godot 4.7.1 .NET and run `scenes/TestArena.tscn`.

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

The fixed arena contains Feral, Spitter, and Brimstone Colossus. Boss death
enters Map Complete; the optional Atlas adapter is not part of the main arena.

## Tests and smoke scenes

Run the pure rules suite with:

```powershell
dotnet test tests\Arpg.Domain.Tests\Arpg.Domain.Tests.csproj --no-restore -c Release
```

The Domain suite is split by system (`CombatMathTests`, `LootGeneratorTests`,
`EquipmentTests`, `SkillSupportTests`, `MapScalingTests`, and
`SaveValidationTests`) instead of one monolithic test file.

Godot smoke scenes are named `Milestone4Smoke.tscn` through
`Milestone20ContentRuntimeSmoke.tscn`. They cover enemy behavior, equipment,
flow states, map events, supports, passive allocation, content save/restore,
map modifiers, Atlas progression, Stash, and Crafting.

## Migration boundaries

Gameplay rules and portable content data live in `src/Arpg.Domain`. Godot
scenes, Nodes, resources, UI, InputMap, telegraphs, and particles live in the
root project. Deferred large systems such as procedural maps, encounter
composition, the full Boss catalogue, and advanced exceptions are outside the
first vertical slice.

The local Windows console runner currently crashes with a native signal 11
before printing scene smoke output; editor C# builds and the Domain suite are
independent validation gates.
