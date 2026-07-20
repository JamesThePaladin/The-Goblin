# The Goblin — Rain World custom slugcat

A SlugBase 2 / BepInEx custom slugcat. Small, fast, hoards shiny things, starts friendly with scavengers.
Full design + current state + decompile checklists live in **`PLAN.md`** — read it before starting work.

## Non-negotiables
- **Target .NET Framework 4.8** (`net48`). Never a newer target — .NET 5+ throws `MissingFieldException` on base-game `List<T>` fields at runtime.
- **Reference `PUBLIC-Assembly-CSharp.dll`** (in `lib/`, sourced from `Rain World/BepInEx/utils`) — NOT the base `Assembly-CSharp.dll`.
- **Multiplayer (Jolly Co-op) is a hard requirement.** EVERY hook must no-op for players who aren't the Goblin — build this in from the first hook, never retrofit. **There is no `IsMe` in SlugBase 2.9.3** (that API name is stale). The real gates: for a feature-keyed hook, `feature.TryGet(player, out val)` *is* the per-player gate — it reads `player.SlugCatClass` internally and returns false for other co-op players; for a hook with no feature to key on, gate on `player.SlugCatClass == <Goblin's SlugcatStats.Name>`; at the game level use `GameFeature<T>.TryGet(RainWorldGame, out val)`.
- Character is multi-instance; mark it so in the constructor.

## Toolchain
- IDE: JetBrains Rider (Arch Linux). Built-in ILSpy decompiler; use Find Usages to navigate game code.
- Game Managed DLLs live in the Proton install under `RainWorld_Data/Managed/`.
- Build output DLL → `mod/plugins/`. Symlink `mod/` into `RainWorld_Data/StreamingAssets/mods/` for hot iteration.
- No debugger attach; use BepInEx console logging.
- SlugBase custom-feature pattern: `PlayerFeature<T>`/`GameFeature<T>` + hook + `TryGet`; re-apply on `SlugBaseCharacter.Refreshed`.

## Current state
**Mechanically COMPLETE and verified in-game (2026-07-19). Only art remains.** `PLAN.md` has
full detail; its STATUS block at the top is authoritative where older prose disagrees.

**Done and verified:**
- **Movement — Rivulet parity, confirmed side-by-side in co-op.** Achieved with ONE detour on
  `Player.isRivulet` (RuntimeDetour — HookGen emits no property-getter hooks), which grants her
  entire land agility kit with her exact constants. No hand-built jump/pounce/wall-jump hooks
  were needed. Her swim stats are name-keyed in `SlugcatStats` and cannot transfer.
- **Wall-crawling — the identity mechanic.** Held on SPECIAL (`input[0].spec`). Gravity
  suspension gated on real terrain (`Tile.wallbehind`, or adjacent `Solid`/`Slope` — so he can't
  hang in mid-air), kinematic vertical movement, hands holding discrete grip points, face turned
  along the crawl direction. Feel is **cling-crawl, NOT wall-running**.
- **Small body** — `body_scale` 0.2 by scaling body *chunks*, not sprites; limbs follow for free,
  so no `DrawSprites` re-anchor was ever needed. Sprites get drawn at vanilla template sizes.
- Stats/diet/karma in JSON, custom tail texture, no heavy-carry lockout, co-op selectable.

**Remaining — all art, none blocked on code:**
1. **`HeadA` (18 frames, ears drawn in)** — the critical path. Procedural ears were built and
   deliberately dropped (far ear rendered in front of the head; they led the direction of travel).
2. The rest of the sheet — body, hips, arms, legs, face.
3. Then crawl **animation states** (`DownOnFours` horizontal, `StandUp` vertical) — decided, unbuilt.

**Art notes:** DMS templates are already on disk at
`workshop/content/312520/2948971756/dressmyslugcat/`. `#000000` renders transparent — use
`#0E0202`. Keep the body white so drawn colours survive the multiply; `00FFFF` is UI/icon only.

## ⚠️ The rule that cost the most time
**Anything vanilla recomputes every frame will beat a value you set alongside it. Own the final
value.** This burned four attempts across three subsystems — `self.gravity` (reassigned in
`Player.Update`, and the value read after `orig` is NOT what `BodyChunk.Update` applied),
`hand.mode`/`absoluteHuntPos` (`SlugcatHand.Update` reassigns and consumes both in one pass), and
ear sprite parenting. What worked every time was setting the end result directly: `chunk.pos.y`,
`hand.pos`. Full table in `PLAN.md` Feature 4.

## Workflow
- **SlugBase hot-reloads character JSON; the plugin DLL does NOT.** After any rebuild the game
  needs a full restart, or you're testing the old DLL while JSON changes appear to work.
- Build with `dotnet build -c Release` from the repo root; the post-build step deploys to
  `mod/plugins/`. BepInEx logs to `BepInEx/LogOutput.log` (`AppendLog` is on); managed exceptions
  land in `exceptionLog.txt` in the game root.
- **BepInEx only injects under Proton with launch options** `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
  If the Goblin vanishes entirely, check that before suspecting the mod.

## Working style
- When reading decompiled code, rename cryptic locals and explain logic before changing anything.
- Ask before destructive actions. Prefer small, verifiable steps.
- Don't mark movement as complete until Rivulet parity is actually verified in-game.
