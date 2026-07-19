# The Goblin — Rain World custom slugcat

A SlugBase 2 / BepInEx custom slugcat. Small, fast, hoards shiny things, starts friendly with scavengers.
Full design + current state + decompile checklists live in **`PLAN.md`** — read it before starting work.

## Non-negotiables
- **Target .NET Framework 4.8** (`net48`). Never a newer target — .NET 5+ throws `MissingFieldException` on base-game `List<T>` fields at runtime.
- **Reference `PUBLIC-Assembly-CSharp.dll`** (in `lib/`, sourced from `Rain World/BepInEx/utils`) — NOT the base `Assembly-CSharp.dll`.
- **Multiplayer (Jolly Co-op) is a hard requirement.** EVERY hook must gate on `IsMe(Player)` / `IsMe(RainWorldGame)` and no-op for other players. Build this in from the first hook, never retrofit.
- Character is multi-instance; mark it so in the constructor.

## Toolchain
- IDE: JetBrains Rider (Arch Linux). Built-in ILSpy decompiler; use Find Usages to navigate game code.
- Game Managed DLLs live in the Proton install under `RainWorld_Data/Managed/`.
- Build output DLL → `mod/plugins/`. Symlink `mod/` into `RainWorld_Data/StreamingAssets/mods/` for hot iteration.
- No debugger attach; use BepInEx console logging.
- SlugBase custom-feature pattern: `PlayerFeature<T>`/`GameFeature<T>` + hook + `TryGet`; re-apply on `SlugBaseCharacter.Refreshed`.

## Current state
Salvaged from the old repo and rebuilt on a fresh SlugTemplate. See `PLAN.md` for detail.

**Done (JSON):**
- Baseline speed stats (walk/climb/tunnel + weight/lung/loudness).
- Friendly-with-scavengers via built-in `alignments` — **losable, not locked**: `{ "like": 1, "strength": 1 }` (starts fully friendly, can drop if he wrongs them) + maul blacklist + diet stun-on-eat.

**Not done — real work remaining:**
- **Keep-up movement (Rivulet's pace + jump kit)** — C# HOOK work, not JSON (jump height, pounce, and the 2D side-wall wall-jump are not SlugBase JSON features). Goal: keep up with Rivulet in co-op without cloning her; his identity is wall-walking. Pattern = the template's `super_jump` example: `PlayerFeature<float>` + a `Player.Jump` hook scaling `jumpBoost`. Order: jump height first (it feeds pounce + the side-wall wall-jump), then those. Decompile: Find Usages on the Rivulet `SlugcatStats.Name` enum to read her real jump/speed values AND code paths; match feel, exclude water. NOTE: side-wall wall-jump (2D vertical surfaces) is this feature; lizard wall-walk (climbing background/terrain) is the separate feature below — don't conflate.
- **Smaller body** — art (custom sprite atlas, drawn from scratch) **+ `PlayerGraphics` code**. Arms/legs are positioned procedurally in `PlayerGraphics.DrawSprites` relative to body chunks, so a smaller body makes limbs emerge from the wrong spot ("arms out the cheeks") unless the hand/leg sprite positions are offset in that hook — drawing smaller RE-triggers the anchor problem, it doesn't avoid it. **Sprite integration DECIDED: self-contained** (`FAtlas` + `PlayerGraphics.DrawSprites`), no DMS — the same hook does the limb re-anchor (one hook, two jobs). Color intent: `00FFFF` = UI/icon; body is white (`custom_colors` Body ≈ white) with a hand-drawn cyan stripe (color is multiply-style, so a white body preserves drawn colors); `#000000` = transparent, use `#0E0202`. Grab the DMS `ModTemplate.zip` for templates even though we're off-DMS. Select-menu scene art already done.
- **Wall-walking like lizards** — HARD, greenfield; this is the Goblin's movement identity. Goal: lizard-grade seamless wall↔floor flow with NO speed cost (rip lizard traversal exactly if feasible); cling-crawl is the fallback. Study lizard terrain-grip code first. Shares `Player` movement surface with the speed work — sequence after it.

**JSON cleanup outstanding:**
1. `auto_grab_batflies` must be boolean `true`, not the string `"true"`.
2. Remove both `select_menu_scene` and `select_menu_scene_ascended`, and the broken ghost scene JSON — **Goblin is Jolly Co-op-only for now**, campaign-select presentation not wired up. KEEP the select-scene art PNGs for later re-enabling. Rename the 8 multiplayer portrait PNGs off `-prototype` (co-op still uses those).

**Access / world:** Jolly Co-op-only for now. Co-op players run in the HOST's campaign world, so the Goblin's own `world_state` and `start_room` are **deferred/irrelevant** until he becomes a standalone campaign character (plan then: Survivor/White baseline; avoid Saint/frozen + Rivulet/flooded). Note: removing the select scenes removes the *art*, not necessarily single-player-carousel access — no clean SlugBase "co-op-only" flag was found, but that doesn't matter for a WIP. Confirm the custom slugcat is actually selectable as a Player-2+ in Jolly Co-op.

## Working style
- When reading decompiled code, rename cryptic locals and explain logic before changing anything.
- Ask before destructive actions. Prefer small, verifiable steps.
- Don't mark movement as complete until Rivulet parity is actually verified in-game.
