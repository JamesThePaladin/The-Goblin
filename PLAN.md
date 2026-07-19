# Custom Slugcat — Build Plan

*A smaller, faster slugcat that starts friendly with scavengers and can climb walls like a lizard. Built on SlugBase 2 / BepInEx, targeting multiplayer (Jolly Co-op).*

Planning draft — Jul 2026. Verify the "Preflight" section against the live game before investing time.

---

## Salvaged from `JamesThePaladin/The-Goblin` (inspected)

The repo was reviewed. It's a SlugTemplate clone; author id **`sanctus.thegoblin`**, character **"The Goblin"** — *"A small slugcat with big ol' ears. He enjoys finding shiny things and storing them in his claimed shelter."* Design tagline: small and fast. This is the same character; keep the concept.

**Code side = clean slate.** `src/Plugin.cs` and `Extras.cs` are the untouched template (all example hooks commented out, `LoadResources` empty). Namespace is still `SlugTemplate` (cosmetic; rename on rebuild). MOD_ID is correctly `sanctus.thegoblin`. Nothing to salvage from code — nothing was written.

**All the value is in the JSON, and it's good.** Every feature key used is a confirmed **built-in** — nothing needs an extra dependency. Two of the three target features are already fully solved here:

- **Baseline speed → DONE in JSON:** `walk_speed`, `climb_speed`, `tunnel_speed` all `[1.2, 1]`; `weight` `[0.8, 0.6]` (light); `lung_capacity` `[1.2, 1]`; `loudness` `[0.8, 0.6]` (quiet, fits a small sneak). *(This is generic faster movement only — NOT the full Rivulet movement kit, which is a separate code feature and is not done.)*
- **Friendly with scavengers → DONE in JSON** (mechanism resolved): the built-in `alignments` feature handles it. **Design choice made:** drop `locked` so the friendliness is *losable* — start maxed but let it drop if the Goblin wrongs them ("gotta respect the scavs; don't want to get caught lackin'"). Corrected value: `{ "like": 1, "strength": 1 }` — starts fully friendly on a new save, then normal reputation dynamics apply. Still reinforced by `maul_blacklist: ["Scavenger"]` and the `diet` stun-on-eat override.
- Plus extras already dialed in: `throw_skill: [2,1]` (Hunter-tier), `back_spear`, `can_maul` + `maul_damage: 1.5`, `karma: 2`, `karma_cap: 6`, `auto_grab_batflies`, color `00FFFF` (cyan), `diet` base `Red` (omnivore-tuned via corpses/plants `1.0`).

**One JSON bug to fix when rebuilding:**
1. `auto_grab_batflies` is written as the string `"true"` — it's a **boolean**, so it must be `true` (no quotes) or SlugBase may reject/ignore it.

*(The old ascended/ghost-scene mismatch is now moot — both scene features are being removed, see below.)*

**Scenes/art present (now unwired):** the repo has a working select-menu scene (`slugcat_goblin.json` → `Slugcat_Goblin`: Background + Slugcat + layered Grass, tuned depths) and 8 multiplayer portrait PNGs (still `-prototype` named). **Decision: Goblin is Jolly Co-op-only for now, so both `select_menu_scene` and `select_menu_scene_ascended` are removed** and the broken ghost scene isn't needed. **Keep the select-scene art PNGs** — they're good work and re-wiring them later (for full campaign access) is just re-adding the two feature lines + the scene JSON. The multiplayer portraits *are* still relevant (co-op uses them), so rename those from `-prototype`. The in-game **sprite atlas** (the "smaller" body) is still not in the repo — still to do.

### Corrected `the_goblin.json` (drop-in for the fresh template)

```json
{
	"id": "Goblin",
	"name": "The Goblin",
	"description": "A small slugcat with big ol' ears. He enjoys finding shiny things and storing them in his claimed shelter.",
	"features": {
		"color": "00FFFF",
		"custom_colors": [
			{ "name": "Body", "story": "FFFFFF" },
			{ "name": "Eyes", "story": "000000" }
		],
		"auto_grab_batflies": true,
		"weight": [0.8, 0.6],
		"tunnel_speed": [1.2, 1],
		"climb_speed": [1.2, 1],
		"walk_speed": [1.2, 1],
		"crouch_stealth": [1.2, 1],
		"throw_skill": [2, 1],
		"lung_capacity": [1.2, 1],
		"loudness": [0.8, 0.6],
		"alignments": {
			"Scavengers": { "like": 1, "strength": 1 }
		},
		"diet": {
			"base": "Red",
			"corpses": 1.0,
			"plants": 1.0,
			"overrides": { "Scavenger": -1 }
		},
		"back_spear": true,
		"can_maul": true,
		"maul_blacklist": [ "Scavenger" ],
		"maul_damage": 1.5,
		"karma": 2,
		"karma_cap": 6
	}
}
```

*(Both `select_menu_scene` and `select_menu_scene_ascended` axed — Goblin is Jolly Co-op-only for now, so no campaign select presentation is wired up. Keep the scene **art PNGs** in the repo so campaign access + select art can be re-enabled later by just re-adding these two lines and the scene JSON.)*

*Color intent: `color` `00FFFF` drives the **UI/icon** cyan; `custom_colors` Body is set **white** so the hand-drawn cyan body stripe shows (color is applied multiply-style, so a white body preserves drawn colors). Eyes black as a placeholder. Confirm the exact `color` vs Body vs UI mapping in SlugBase when testing — adjust if UI cyan and body-white don't resolve independently.*

**Net effect:** salvage removes the whole speed/scavenger JSON tier. What remains to build: the **movement hooks** (jump kit), the **sprite atlas + limb re-anchor** (art **and** `PlayerGraphics` code), and **wall-walking** — plus the one `auto_grab_batflies` fix. Because he's **Jolly Co-op-only for now**, `world_state`, `start_room`, and the select-menu presentation are all deferred (co-op players run in the *host's* world).

---

## Concept summary

| Feature | Tier | Nature | Status |
|---|---|---|---|
| Baseline speed stats | Easy | JSON | ✅ Already written in salvaged JSON |
| Friendly with scavengers | Easy | JSON via `alignments` | ✅ Written; **change from `locked` → losable** |
| **Keep-up movement (Rivulet's pace + jump kit)** | **Hard** | **C# hooks** (like the `super_jump` example) | ❌ Hook work — jump height isn't JSON |
| Smaller | Moderate–Hard (art **+ code**) | Sprite atlas + `PlayerGraphics.DrawSprites` limb re-anchor | Select scene done; body atlas + limb offset TODO |
| Wall-walking (lizard-flow) | Hard | Custom player movement | The real project — the movement identity |

**Design intent on movement (updated):** the goal is **NOT** to clone Rivulet wholesale — the Goblin just needs to **keep up** with her in co-op (his friends main her for speed) while having his own identity (wall-walking). But "keep up" is **not** achievable in JSON alone: the base speed multipliers are in JSON, but **jump height, pounce, and the side-wall wall-jump are not SlugBase JSON features** — they require C# hooks, following the same pattern as the template's `super_jump` example (`PlayerFeature<float>` + a `Player.Jump`-style hook). Jump power also *feeds* pounce and the wall-jump, so the jump hook comes first and the others build on it.

So: JSON gets the general speed; **hooks** get the jump kit that lets him actually keep pace. Match her *feel/pace*, not every ability. Water movement excluded regardless.

**Two different "wall" mechanics — don't conflate:**
- **Side-wall wall-jump** — the existing 2D vertical-surface jump. Part of *this* movement feature, powered by the jump hook.
- **Lizard wall-walk** — climbing background/terrain surfaces. The *separate* identity feature (Feature 4).

Remaining real work: Rivulet movement parity, the sprite atlas, and wall-walking — plus two small JSON fixes.

---

## Core principle: multiplayer-first

This is being built for Jolly Co-op, so **every hook gates on "is this actually my slugcat" from the very first one** — don't retrofit it later. **Note (verified against SlugBase 2.9.3): there is no `IsMe` method** — that API name is stale. The real pattern:
- **Feature-keyed hook:** `feature.TryGet(player, out val)` *is* the gate. Its decompiled body reads `player.SlugCatClass`, looks up that name's `SlugBaseCharacter`, and returns true only if that character has the feature — so it's automatically false for other co-op players (Rivulet, Survivor, …).
- **Hook with no feature to key on:** compare `player.SlugCatClass` against the Goblin's registered `SlugcatStats.Name`.
- **Game-level:** `GameFeature<T>.TryGet(RainWorldGame, out val)`.

Mark the character multi-instance in its constructor and make each hook a no-op for any player that isn't this slugcat.

Co-op facts that matter for design:
- Each slugcat has its **own reputation counter and gain rate** in co-op. Good — the friendly-with-scavengers feature will apply to this slugcat specifically, not the whole lobby.
- Player 1 in a DLC/Downpour campaign is locked to the campaign's default slugcat; other players can pick freely. Relevant only if you tie the character to a specific campaign's worldstate.
- Slugcats collide with each other; the co-op "Size → Slugpup" toggle is purely cosmetic (not related to your custom sprite work).

---

## Environment (recap, for the Arch box)

- **IDE:** JetBrains Rider (cross-platform, free non-commercial, built-in ILSpy decompiler — F12 into game classes).
- **Target framework:** **.NET Framework 4.8**, dev pack (not runtime). Pin `<TargetFramework>net48</TargetFramework>`. Targeting .NET 5+ throws `MissingFieldException` at runtime on base-game `List<T>` fields.
- **References:** the game's Managed DLLs (`Assembly-CSharp.dll`, `UnityEngine.*.dll`) from `.../steamapps/common/Rain World/RainWorld_Data/Managed/` (inside the Proton install). Set `Private=false` so they don't copy into build output.
- **Start from:** SlimeCubed's `SlugTemplate` (pre-wired with SlugBase, a BepInPlugin entry point, an OnModsInit wrapper, and example JSON features). Rename solution/project, set mod ID `author.mod`, sync `modinfo.json` ID/Name/version.
- **Iteration tip:** symlink the `mod` folder into `RainWorld_Data/StreamingAssets/mods/` so builds and JSON hot-reloads land automatically. SlugBase hot-reloads its JSON registries on save (files present at startup only; new files need a restart). JSON/feature parse errors surface via the SlugBase icon top-left — hover for the inner exception.
- **Debugging:** no easy debugger attach; lean on BepInEx console logging.

**Custom-feature pattern** (everything past the JSON stats uses this): define a `public static readonly PlayerFeature<T>` (or `GameFeature<T>`), add its ID to the slugcat JSON `features`, hook the relevant method, and run your code only when `feature.TryGet(...)` succeeds. If a feature needs to re-apply on JSON reload, subscribe to `SlugBaseCharacter.Refreshed`.

---

## Reading the decompiled code

Expect the raw output to look rough (lost local names → `num`, `num2`, `flag`, `array5`; coroutines → switch-based state machines). That's normal and largely fixable. Levers, roughly in order of impact:

**Reference vs. read — two different assemblies.**
- **Compile against** `PUBLIC-Assembly-CSharp.dll` (in `Rain World/BepInEx/utils`). It exposes every field/method signature as public. Referencing the base `Assembly-CSharp.dll` instead breaks compilation on non-public members.
- **Read bodies** by decompiling the real `Assembly-CSharp.dll` in `RainWorld_Data/Managed/`.

**Decompiler choice — dnSpy and ILSpy fail in opposite ways.**
- ILSpy (and Rider, which uses the ILSpy engine): clean string-`switch` statements, but sometimes collapses logic into monstrous single-line ternary chains (`SSOracleBehavior.NewAction()` is one 3000+ char line).
- dnSpy: renders those chains as readable if/else, but turns string switches into ugly hash-and-binary-search blocks (`RainWorld.LoadSetupValues()` → 909 lines, 15 tabs deep).
- Practical rule: when a Rider decompile gives a giant ternary one-liner, that's the tool, not you — paste it here and it gets unrolled. For AI-heavy classes (lizard behavior), having dnSpy as a second opinion can help.

**On Arch specifically.**
- Rider's built-in decompiler is the pragmatic default (already coding there; F12 to navigate, Find Usages to trace).
- ProjectRover = cross-platform ILSpy-based standalone viewer if a dedicated tool is wanted.
- Security: only get dnSpy from the official `dnSpy/dnSpy` or `dnSpyEx/dnSpy` GitHub repos — trojaned fakes exist.

**Navigation, not top-to-bottom reading.**
- Use **Analyze / Find Usages** to trace a field or method to every place it's touched. This is how to approach something huge like `Player.MovementUpdate` without reading it linearly.
- Because Rain World runs on the Futile wrapper rather than deep Unity integration, the codebase is essentially self-contained — little hidden engine behavior, so the decompiler shows nearly the whole picture.
- Reference: Rain World Wiki → "Reading code" page.

**The paste-to-Claude loop.** Find Usages to isolate the relevant slice → paste the gnarly method → get back a renamed, commented version with the logic in plain English. For the lizard climbing code, the annotation will separate reusable tile/terrain-grip *concepts* from lizard-AI-specific noise that won't port to player physics.

---

## Feature 1 — Movement

### 1a. Baseline speed stats ✅ (done in salvaged JSON)
- Already set: `walk_speed`, `climb_speed`, `tunnel_speed` = `[1.2, 1]`, `weight` `[0.8, 0.6]`. Built-in, valid. This is generic "faster," and it's complete.

### 1b. Keep-up movement — Rivulet's pace + jump kit (C# hooks) ❌
**The design goal:** the Goblin keeps up with Rivulet in co-op without being a clone. But this is **hook work, not JSON tuning** — jump height, pounce, and the side-wall wall-jump are **not** SlugBase JSON features. The base speed multipliers (already in JSON) are only part of it.

**The pattern (per the template's own example):** the commented-out `super_jump` in `Plugin.cs` is the model — `public static readonly PlayerFeature<float> SuperJump = PlayerFloat("thegoblin/super_jump")`, added to the slugcat JSON, applied by hooking `Player.Jump` and scaling `jumpBoost` when `SuperJump.TryGet(...)` succeeds. Jump height is done exactly this way.

**Build order within movement (jump feeds the rest):**
1. **Jump height** first — a `Player.Jump` hook to reach Rivulet's height. Uncomment/adapt the `super_jump` example as the starting point.
2. **Pounce** and **side-wall wall-jump** build on that jump power — implement after the jump is right, since they inherit from it.
3. Tune against Rivulet's real numbers (see decompile below).

**Decompile purpose (revised):** Find Usages on the Rivulet enum (`MoreSlugcatsEnums.SlugcatStatsName.Rivulet`) to read **both** her actual jump/movement *values* **and** the *code paths* — so the Goblin's hooks can match her feel rather than guess. (Last turn I understated this as "just read values"; it's read values **and** replicate the mechanics via hooks.)

**Note on Rivulet's exact kit:** confirm what her movement actually comprises from the Find-Usages list rather than assuming — the branches the game gates on `Rivulet` are the authoritative inventory of jump/pounce/wall-jump/etc. Water branches stay excluded.

**Decompiled-code checklist:**
- [ ] Find Usages on the Rivulet `SlugcatStats.Name` value → full movement branch list + her jump/speed constants.
- [ ] `Player.Jump` — the hook surface for jump height (mirror the `super_jump` pattern).
- [ ] Pounce handling and the existing 2D side-wall wall-jump — how they read jump power, so the Goblin's version stays consistent.
- [ ] Flag water/swim branches as **excluded**.
- [ ] Sequence vs Feature 4 wall-walk (both touch `Player` movement).

---

## Feature 2 — Friendly with scavengers ✅ (done in salvaged JSON)

**Resolved:** the built-in `alignments` game feature sets default community reputation. **Decision made — losable, not locked:**
```json
"alignments": { "Scavengers": { "like": 1, "strength": 1 } }
```
- On a new save, each community's reputation moves toward `like` by fraction `strength`. With `strength: 1` and no `locked`, the Goblin **starts fully friendly but can lose it** if he wrongs the scavs — which is the intent ("gotta respect the scavs"). **No hook needed.**
- Backed up by `maul_blacklist: ["Scavenger"]` and a `diet` override (`Scavenger: -1` → eating one stuns you).
- In co-op each slugcat has its own reputation counter, so this is the Goblin's standing specifically.

**Optional taste tweak:** if starting *fully* maxed feels too generous, `like: 0.8, strength: 0.6` gives a warm-but-earnable start. One-line either way.

---

## Feature 3 — Smaller (art + PlayerGraphics code)

Custom **sprite atlas** (smaller body) **plus `PlayerGraphics` work** to fix limb placement. Not purely art — see below. Reference guide: *Dress My Slugcat: Custom Slugcat Tutorial* (Teno Al Mehri et al.). Starting the sprite from scratch (the old base came from a since-lost body-size-editing mod).

### The "arms out the cheeks" problem — it's procedural, and smaller RE-triggers it
Correction to an earlier assumption: drawing smaller does **not** sidestep the anchor problem — it **re-causes** it, inversely. The arms (hands) and legs are **not positioned by the sprite**; they're placed **procedurally in `PlayerGraphics.DrawSprites`** relative to the body chunks. Shrink the body art and the limb attachment points *don't* move with it, so hands reach from where a full-size body would be → arms appear to emerge from the cheeks.

So the fix is code, and it lives in the **same `PlayerGraphics.DrawSprites` hook** used to assign the self-contained atlas (below): offset/re-anchor the hand and leg sprite positions to match the smaller body. Two-birds: atlas assignment and limb re-anchoring are one hook.

- Border-expansion rules still apply if you *do* expand a canvas (arms → height only, legs → width only, head/face → both, body/hips → never), and pixels must stay inside frame borders. But the Goblin's core issue is the **procedural limb offset**, not border expansion.
- Arm "Obstruction Point" at the low bottom of the frames (arms cross when receding) still applies.

### Sprite fundamentals (from the guide)
- Slugcat = **6 sprite parts, ~87 vanilla frames** (PlayerArm 15, HeadA 18, LegsA 32, FaceA/B 20, BodyA 1, HipsA 1). **Frame counts are fixed.**
- Sprites are **white because color is applied in code (multiply-style)**.
- **Color intent (corrected):** `00FFFF` is the **UI/icon** color, not the body. Body is **mostly white with a hand-drawn cyan stripe.** To make that show, keep the body part **white** (set `custom_colors` Body ≈ white so the drawn cyan isn't tinted away) and draw the stripe on the sprite. Confirm the exact SlugBase mapping of `color` vs `custom_colors` Body vs UI when testing.
- **Pure black `#000000` renders as transparent** — use `#0E0202` for dark markings.
- Idle head/face expected **roughly symmetrical** (they flip when moving); heavy asymmetry needs the asymmetric template.

### Integration — self-contained (DECIDED, no DMS)
Not using DMS. Register an `FAtlas` in the plugin, drop `png` + `txt` into `mod/atlases/`, assign via a `PlayerGraphics.DrawSprites` hook (replaces existing parts only). No external dependency, fully bundled — better for co-op robustness, and it's the same hook the limb re-anchoring needs. Still grab the DMS `ModTemplate.zip` for its labeled templates + `.txt` files (saves manual work even off-DMS). Mechanics documented on the "Custom Player Graphics (without DMS)" wiki page.

### Decompiled-code checklist
- [ ] `PlayerGraphics.DrawSprites` — how arm (hand) and leg sprites are positioned relative to body chunks; where to offset them for a smaller body. **This is the real work of this feature.**
- [ ] Confirm sprite-leaser indices for the hand/leg sprites so the offsets target the right ones.
- [ ] `FAtlas` registration pattern (from the wiki page, not the decompile).

---

## Feature 4 — Wall-walking (hard; the movement identity)

**Design goal, sharpened:** this is the Goblin's signature movement. The target is what lizards do — **effortlessly flow between walls and floors with no cost to speed**, seamless surface transitions. Rip the lizard traversal *exactly* if feasible, not a slow sticky approximation. **Feel decision (tentative): cling-crawl** as the fallback, but the real aim is lizard-grade fluid flow — we decide for real once the code is in front of us.

The player doesn't natively free-climb the way lizards grip terrain, and lizard climbing is driven by their creature AI — not obviously portable to player physics. But the logic *is* in the codebase, so the question is how much of it can be lifted wholesale vs. adapted. Because the goal is now "exact rip" rather than "approximate," the lizard-code study is even more central.

- **Task 0 (before any movement code):** read the lizard climbing/terrain-grip code. How they detect climbable tiles, choose grip points, and transition wall↔floor **without losing speed**. Determine how much is directly reusable for a `Player`.
  - Look at: `Lizard` AI / movement, base `Creature` terrain-traversal, how they evaluate `Room.Tile` terrain for grip.
- **Then:** custom player movement — detect climbable adjacent terrain tiles, apply a stick / anti-gravity force, and handle movement while attached. Primary hook target: `Player.MovementUpdate` (confirm the current method in the decompiled code).
- **Feel (tentative — confirm against code):**
  - [x] **Cling-crawl** as the working baseline / fallback.
  - [ ] But aim higher: lizard-grade seamless wall↔floor flow with no speed penalty. Decide how close we can get once the lizard code is read.
- **Multiplayer:** all of the above must gate per-player (feature `TryGet(player)`, or a `player.SlugCatClass` compare — see "multiplayer-first"; no `IsMe` in SlugBase 2.9.3), and stick forces / attachment state need to behave with multiple players and player-player collision.
- **Parked topic (James to explain at the machine):** how Rain World *backgrounds* work — reportedly beautiful and slightly complex. Relevant to how rooms/surfaces are represented; James will break this down when we're in the code.
- **Decompiled-code checklist:**
  - [ ] Lizard terrain-grip logic — reusable tile-evaluation concepts.
  - [ ] `Player.MovementUpdate` and related physics (gravity application, body-chunk handling) — the hook surface.
  - [ ] How `Room.Tile.TerrainType` is read, to define "climbable."

---

## Preflight — verify at the machine before investing

- [x] **Salvage `JamesThePaladin/The-Goblin`** — done (see salvage section). JSON recovered + corrected; code was untouched template.
- [ ] **Apply the two JSON fixes** — `auto_grab_batflies` → boolean `true`; fix or remove the ascended/ghost scene.
- [ ] **SlugBase stability post-1.5 / The Watcher.** The DLC + 1.5 update reportedly broke some SlugBase custom-feature and arena functionality around late 2025. Confirm the current SlugBase release is stable against the installed game version.
- [ ] **Jolly Co-op state.** As of the 1.5 update, Jolly Co-op is officially supported in The Watcher campaign and retroactively across all five Downpour campaigns (supersedes the buggy community "Jolly Co-op for the Watcher" mod). Confirm current status and that a **custom SlugBase slugcat** works in co-op (multi-instance / per-player feature gating).
- [ ] **Worldstate/timeline — DEFERRED while co-op-only.** Co-op players run in the *host's* campaign world, so the Goblin's own `world_state`/`start_room` don't drive anything yet. (When he later becomes a standalone campaign character: inherit Survivor/White baseline; avoid Saint/frozen and Rivulet/flooded; set explicitly. Watcher-era note: timelines are distinct from slugcats and custom ones don't inherit spawns/connections as cleanly.)
- [x] **Co-op selectability — SOLVED (verified against decompile).** The Goblin loaded fine but was NOT selectable in Jolly Co-op. Root cause: `JollyCoop.JollyMenu.JollyPlayerSelector` filters slugcats by `!SlugcatStats.HiddenOrUnplayableSlugcat(name) && SlugcatStats.SlugcatUnlocked(name, rainWorld)`. `HiddenOrUnplayableSlugcat` returns false for "Goblin" (fine), but `SlugcatUnlocked` returns false — and **SlugBase does not hook `SlugcatUnlocked`** (it only handles Expedition's `CheckUnlocked`). So a custom co-op-only slugcat with no campaign reads as *locked*. Note: the single-player select carousel is different — SlugBase's `SetSlugcatColorOrder` hook force-adds every registered character there gated ONLY by `HiddenOrUnplayableSlugcat`, and does NOT require a `select_menu_scene` (so removing the scenes did not remove carousel access — confirms the earlier assumption). **Fix shipped:** hook `On.SlugcatStats.SlugcatUnlocked` in `Plugin.cs` → return true for `i.value == "Goblin"`, else `orig`. Quick no-code way to test the same thing: enable the MSC Remix option "Unlock all campaigns" (`chtUnlockCampaigns`), which short-circuits `SlugcatUnlocked` to true for everything.
- [x] **Sprite guide** — gathered: *Dress My Slugcat* tutorial (folded into Feature 3). Also grab the DMS `ModTemplate.zip` (labeled templates + `.txt` files) regardless of DMS-vs-self-contained choice.
- [x] **Sprite integration decided:** self-contained (`FAtlas` + `PlayerGraphics.DrawSprites`), no DMS. Same hook also handles the smaller-body limb re-anchoring.
- [ ] **Decompile pass.** Open the game assembly in Rider's decompiler and pull up the four checklists above together.

---

## Suggested build order

1. **Bare slugcat** — drop the corrected salvaged JSON into a fresh SlugTemplate, confirm it loads in the Remix menu and boots a campaign. This brings in baseline speed + friendly-with-scavengers (both done in that JSON) and validates the toolchain in one step.
2. **JSON cleanup** — fix `auto_grab_batflies` → boolean `true`; remove both `select_menu_scene*` lines and the ghost scene (co-op-only for now); rename the multiplayer portrait PNGs off `-prototype`.
3. **Smaller (sprite atlas + limb re-anchor)** — draw the smaller body from scratch, register the `FAtlas`, and in the **same `PlayerGraphics.DrawSprites` hook** offset the hand/leg positions so limbs don't emerge from the cheeks. Art and this one hook together. Sits partly on the code track now (not fully independent), but doesn't depend on the movement hooks.
4. **Keep-up movement (Rivulet's pace + jump kit)** — first code feature, and hook work. Order: jump-height hook (adapt the `super_jump` example) → pounce/side-wall wall-jump on top of it → tune to Rivulet's real numbers from the Find-Usages pass. Gate per-player (feature `TryGet(player)`). Both this and wall-walking touch `Player` movement, so do this first and keep the hooks tidy for #5.
5. **Wall-walking (lizard-flow)** — last. Lizard-code study, confirm the feel, prototype in isolation, integrate with per-player gating and the movement hooks.

---

## Open decisions

- **Movement** ✅ *decided:* not a Rivulet clone, but "keep up" is **hook work, not JSON** — jump height (via a `super_jump`-style `Player.Jump` hook) plus pounce and the side-wall wall-jump that build on it. Decompile reads her values **and** code paths. Water excluded. (Side-wall wall-jump ≠ the lizard wall-walk in Feature 4.)
- **Scavenger friendliness** ✅ *decided:* start fully friendly but **losable** (`like:1, strength:1`, no `locked`).
- **Wall-walking feel** ✅ *tentative:* cling-crawl baseline, aiming for lizard-grade seamless wall↔floor flow at no speed cost — final call after reading the lizard code.
- **Access** ✅ *decided:* Jolly Co-op-only for now. Both select-menu scenes removed (art PNGs kept for later). Consequence: `world_state` + `start_room` deferred (co-op uses host's world), and campaign-carousel presentation is unwired.
- **World state** ⏸️ *deferred:* Survivor/White baseline is the plan *when* he becomes a standalone campaign character; irrelevant while co-op-only.
- *Still open:* `start_room` (deferred with world_state); exact water-exclusion boundary for movement; how faithful the wall-flow can be (code-dependent). *(Co-op selectability resolved — see Preflight; fixed via a `SlugcatUnlocked` hook.)*
