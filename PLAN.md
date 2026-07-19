# Custom Slugcat — Build Plan

*A smaller, faster slugcat that starts friendly with scavengers and can climb walls like a lizard. Built on SlugBase 2 / BepInEx, targeting multiplayer (Jolly Co-op).*

Planning draft — Jul 2026. Verify the "Preflight" section against the live game before investing time.

---

## Salvaged from `JamesThePaladin/The-Goblin` (inspected)

The repo was reviewed. It's a SlugTemplate clone; author id **`sanctus.thegoblin`**, character **"The Goblin"** — *"A small slugcat with big ol' ears. He enjoys finding shiny things and storing them in his claimed shelter."* Design tagline: small and fast. This is the same character; keep the concept.

**Code side = clean slate.** `src/Plugin.cs` and `Extras.cs` are the untouched template (all example hooks commented out, `LoadResources` empty). Namespace is still `SlugTemplate` (cosmetic; rename on rebuild). MOD_ID is correctly `sanctus.thegoblin`. Nothing to salvage from code — nothing was written.

**All the value is in the JSON, and it's good.** Every feature key used is a confirmed **built-in** — nothing needs an extra dependency. Two of the three target features are already fully solved here:

- **Baseline speed → DONE in JSON, retuned 2026-07-19:** `walk_speed` `[1.75, 1]`, `climb_speed` `[1.8, 1]`, `tunnel_speed` `[1.6, 1]` — Rivulet's actual values from the decompile (they were `[1.2, 1]`, i.e. nowhere near her); `weight` `[0.8, 0.6]` (light); `lung_capacity` `[1.2, 1]`; `loudness` `[0.8, 0.6]` (quiet, fits a small sneak). *(This is on-land pace parity only — NOT the jump kit, which is a separate code feature and is not done. See Feature 1a/1b.)*
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

### 1a. Baseline speed stats ✅ (retuned 2026-07-19 to Rivulet's real numbers)
Previously `[1.2, 1]` across the board, which was tuned against nothing and left him
**far** behind Rivulet. Corrected against her actual values, read out of
`SlugcatStats..ctor` in the decompile:

| Stat (game field) | JSON key | Rivulet | Goblin (was → now) |
|---|---|---|---|
| `runspeedFac` | `walk_speed` | **1.75** | 1.2 → **1.75** |
| `poleClimbSpeedFac` | `climb_speed` | **1.8** | 1.2 → **1.8** |
| `corridorClimbSpeedFac` | `tunnel_speed` | **1.6** | 1.2 → **1.6** |
| `bodyWeightFac` | `weight` | 0.95 | 0.8 (unchanged — lighter on purpose) |
| `throwingSkill` | `throw_skill` | 1 | 2 (Goblin better, keep) |
| `lungsFac` | `lung_capacity` | 0.15 | 1.2 (water, irrelevant) |

Also in her block but **water, excluded**: `swimBoostCost` 0.025, `swimBoostCooldown` 10.
These are keyed on slugcat *name* in `SlugcatStats`, so they can never leak to the
Goblin regardless of what we hook.

### 1b. Keep-up movement — Rivulet's jump kit ❌
**The design goal:** the Goblin keeps up with Rivulet in co-op without being a clone.
Her jump constants are hardcoded in `Player`, not JSON — but **not** as scattered
per-ability branches the way this plan originally assumed.

**Key discovery (2026-07-19): the whole kit hangs off one property.** All ~40 of her
movement branches — in `Update`, `UpdateAnimation`, `UpdateBodyMode`, `MovementUpdate`,
`WallJump`, `Jump`, `GrabUpdate`, `LungUpdate` — gate on `Player.isRivulet`:

```csharp
public bool isRivulet {
  get {
    if (ModManager.MSC && SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Rivulet) return true;
    if (ModManager.MSC && ModManager.Expedition && Custom.rainWorld.ExpeditionMode)
      return ExpeditionGame.activeUnlocks.Contains("unl-agility");
    return false;
  }
}
```

That second branch is Expedition's **"Agility" unlock** — the base game already grafts her
entire movement kit onto arbitrary slugcats through this exact property, so it is designed
to be identity-independent. Hooking `On.Player.get_isRivulet` to return true for the Goblin
gets the whole land kit in **one hook**, with no hand-tuning and exact feel parity.

Her jump constants, for reference / if we hand-roll instead:

| Jump | Rivulet | Default | Slugpup |
|---|---|---|---|
| Corridor-exit `jumpBoost` | **14** | 8 | 4 |
| Vertical-pole `vel.y` (chunk 0 / 1) | **9 / 8** | 8 / 7 | 7 / 6 |
| Vertical-pole `vel.x` (chunk 0 / 1) | **9 / 7** | 6 / 5 | 5 / 4.5 |

Impulses are raw constants; the only multiplier is `Mathf.Lerp(1f, 1.15f, Adrenaline)`,
so body weight does **not** scale them.

**GATE CLEARED — audit done 2026-07-19. Verdict: hook it, no suppression needed.**
Every `isRivulet` call site, classified:

| Site | Count | What it does | Land? |
|---|---|---|---|
| `Update` | 7 | Pole-skip / beam vaulting (below) | ✅ want it |
| `UpdateAnimation` | 16 | Animations for the above | ✅ needed |
| `Jump` | 7 | Jump constants (table above) | ✅ want it |
| `WallJump` | 4 | Wall-jump constants | ✅ want it |
| `UpdateBodyMode` | 1 | Slide timing — 10 vs 5 | ✅ want it |
| `MovementUpdate` | 1 | Threshold 8 vs 16 | ✅ want it |
| `GrabUpdate` | 1 | Can grab while submerged on a beam | ~ bonus, suits a hoarder |
| `LungUpdate` | 1 | Drown threshold 0.0 vs -0.3 | ~ water, mild *downside* |

Her pole-skip / beam-vault chain, from the `Update` branches — this is the fluid part:

| Behaviour | Rivulet | Default |
|---|---|---|
| Pole-skip launch `vel.y` (chunk 0 / 1) | **7 / 6** | 4.5 / 3.5 |
| `jumpBoost` off a horizontal pole | **7** | 6 |
| `poleSkipPenalty` after a skip | **3** | 6 |
| Penalty after a *failed* skip | **6** flat | `Min(30, penalty + 12)` |

**There is essentially no water contamination**: her actual swimming power (`swimBoostCost`,
`swimBoostCooldown`, `lungsFac`) lives in `SlugcatStats` keyed on slugcat *name*, so it can
never transfer through this property. `isRivulet` is almost purely the agility kit — which is
exactly why Expedition reuses it as the Agility unlock.

**Implementation gotcha:** MonoMod's HookGen emits **no delegates for property getters** —
7214 `orig_*` types in `HOOKS-Assembly-CSharp.dll`, zero named `get_*`. So
`On.Player.get_isRivulet` does not exist. The getter must be detoured via
`MonoMod.RuntimeDetour.Hook` against the reflected `MethodInfo`, with the `Hook` kept in a
static field so it isn't collected. Done in `Plugin.cs`.

### 1c. No heavy-carry penalty (design call, 2026-07-19) ❌
**Intent:** the Goblin hauls loot all day — grabbing things must not cripple him.

```csharp
public bool HeavyCarry(PhysicalObject obj) {
    if (Grabability(obj) == ObjectGrabability.Drag)     return true;
    if (Grabability(obj) == ObjectGrabability.TwoHands) return true;
    if (obj.TotalMass > this.TotalMass * 0.6f)          return true;   // ← the problem
    if (ModManager.CoopAvailable && obj is Player p)    return !p.isSlugpup;
    return false;
}
```

The mass rule is **relative to the carrier**, and `TotalMass` derives from the body chunks'
mass, seeded from `bodyWeightFac` — i.e. from the `weight` JSON key alone.

**⚠ CORRECTION 2026-07-19 — the original rationale for this hook was wrong.** It was argued
that `weight: 0.8` put his threshold at `0.8 × 0.6 = 0.48` vs Survivor's 0.6, so ~20% more
objects counted as heavy for him. **In-game measurement says `bodyWeightFac` is actually 1.0**
(logged at `Player..ctor`: `mass=0.7`, the vanilla value) — see the open bug below. So he was
never mass-disadvantaged; his threshold is the vanilla 0.6.

The hook is still **wanted**, but on design grounds rather than compensation: a hoarder should
haul loot freely, and `FreeHand()` returns `-1` while heavy-carrying, meaning he cannot hold a
second object at all. That's the real cost, and it's hostile to the character regardless of
mass. Keep the hook; discard the "he's penalised for being light" justification.

**🐞 OPEN BUG — `weight` is not being applied.** Same JSON, same SlugBase hook, same character:
`walk_speed` lands (`runspeedFac=1.75`) but `weight` does not (`bodyWeightFac=1.0`). SlugBase
registers it as `PlayerFloats("weight", 1, 2)` — identical in shape to the speed features — and
applies `bodyWeightFac = ApplyStarve(weight, Mathf.Min(weight[0], 0.9f), ...)`, which should
yield 0.8. Suspicion is `WeightMul.TryGet` returning false; a direct probe is now logged from
`Player..ctor`. Note this also means **the Goblin currently weighs exactly as much as a
Survivor**, which affects jump arcs and momentum — relevant to judging Rivulet parity.

*Not* coupled to Feature 3: the sprite atlas is `PlayerGraphics`-only and never feeds
`TotalMass`. He can be drawn tiny and still weigh whatever `weight` says — visual size and
carry capacity are independent knobs.

Fix: hook `Player.HeavyCarry`, exempt him from the mass rule only, leaving `TwoHands`/`Drag`
objects and other players genuinely heavy so animations don't break.

**Decompiled-code checklist:**
- [x] Find Usages on the Rivulet `SlugcatStats.Name` value → stat constants (1a) + branch inventory (1b).
- [x] `Player.Jump` — read; her constants tabulated above.
- [x] `Player.HeavyCarry` / `Grabability` / `FreeHand` — read; see 1c.
- [x] Audit `isRivulet` call sites for water leakage — cleared, see table.
- [x] `Player.WallJump` — 4 branches; inherited wholesale via the property, no hand-rolling needed.
- [x] Flag water/swim branches as **excluded** — they are, structurally (name-keyed stats).
- [x] Detour confirmed live in-game 2026-07-19 (`Detoured Player.isRivulet` in the BepInEx log, no exceptions); jump/wall-jump/pole feel confirmed good by Sanctus.
- [ ] **Side-by-side pace check against an actual Rivulet in co-op** — the bar `CLAUDE.md` sets for calling movement done.
- [ ] Sequence vs Feature 4 wall-walk (both touch `Player` movement).

**Status:** `Plugin.cs` detours `Player.isRivulet` (RuntimeDetour, per-player gated on
`SlugCatClass`) and hooks `On.Player.HeavyCarry`. Working in-game.

**Workflow gotcha that cost a debugging round:** SlugBase hot-reloads character JSON, the
plugin DLL does **not** — BepInEx loads assemblies once at startup. After any rebuild the
game needs a full restart, or you're testing the old DLL while the JSON changes appear to
work. The symptom is exactly "the stat changes took but the hooks didn't."

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

**Design reference: a fennec fox.** The Goblin's size and silhouette came from picking the
fennec ears in Fancy Slugcats. That's the look to aim at — and it has a concrete consequence
for the scaling work: **a fennec is a small body with *disproportionately large* ears.** So
the ears must NOT inherit `body_scale`; if anything they scale the other way. Keep ear size an
independent parameter from body size (FS had exactly that — separate ear angle/length/width
sliders). Same logic for the head: fennec heads read large relative to the body.

Custom **sprite atlas** (smaller body) **plus `PlayerGraphics` work** to fix limb placement. Not purely art — see below. Reference guide: *Dress My Slugcat: Custom Slugcat Tutorial* (Teno Al Mehri et al.). Starting the sprite from scratch (the old base came from a since-lost body-size-editing mod).

### ✅ RESOLVED 2026-07-19 — scaling the CHUNKS moves the limbs for free
**Tested in-game down to `body_scale` 0.2: no limb misplacement at all.** The section below
was written on the assumption that we'd shrink the *sprite*. We don't — we scale
`bodyChunks[].rad` and the connection distance, and because hands anchor to `bodyChunks[0]`
and legs to the hips chunk, the limb simulation follows automatically. **No `DrawSprites`
re-anchoring is needed.** What this plan called "the real work of this feature" does not exist.

Consequences:
- **Art can be drawn at vanilla template dimensions.** The system does the shrinking, so the
  sprite sheets stay the standard sizes and every frame name/anchor rule still applies as-is.
- The remaining art job is **proportion, not scale** — at 0.2 he reads "chunky" because the
  vertical squash follows the shortened chunk distance while sprite *width* stays native.
  Fix by drawing him thinner, not smaller.
- Sanctus's target size is **0.2** (rad 1.8 / 1.6, distance 3.4).

**Two open artifacts at 0.2:**
- [ ] **Breathing animation looks wrong.** `PlayerGraphics.breath` drives a sprite offset with
  an absolute amplitude, so at 0.2 it's proportionally ~5× too large. Likely fix: scale the
  breath contribution, or clamp it, in a `DrawSprites` hook. (`breath` field at
  `PlayerGraphics`; written in `Update`, consumed in `DrawSprites`.)
- [ ] **Gameplay side effects of a 0.2 hitbox unverified** — he can presumably fit through gaps
  no other slugcat can. May be desirable for a goblin; must be a decision, not an accident.

### (historical) The "arms out the cheeks" problem — it's procedural, and smaller RE-triggers it
*Superseded by the section above — kept because the mechanism analysis is still accurate and
would apply if we ever scaled sprites instead of chunks.*
Correction to an earlier assumption: drawing smaller does **not** sidestep the anchor problem — it **re-causes** it, inversely. The arms (hands) and legs are **not positioned by the sprite**; they're placed **procedurally in `PlayerGraphics.DrawSprites`** relative to the body chunks. Shrink the body art and the limb attachment points *don't* move with it, so hands reach from where a full-size body would be → arms appear to emerge from the cheeks.

So the fix is code, and it lives in the **same `PlayerGraphics.DrawSprites` hook** used to assign the self-contained atlas (below): offset/re-anchor the hand and leg sprite positions to match the smaller body. Two-birds: atlas assignment and limb re-anchoring are one hook.

- Border-expansion rules still apply if you *do* expand a canvas (arms → height only, legs → width only, head/face → both, body/hips → never), and pixels must stay inside frame borders. But the Goblin's core issue is the **procedural limb offset**, not border expansion.
- Arm "Obstruction Point" at the low bottom of the frames (arms cross when receding) still applies.

### Sprite fundamentals (from the guide)
**Source:** *Dress My Slugcat: Custom Slugcat Tutorial* (Teno Al Mehri et al.) —
<https://steamcommunity.com/sharedfiles/filedetails/?id=2902555797>
The guide is written for DMS, but most of its rules are **engine** behaviour and bind us
even though we're self-contained. Split below — check the DMS-only list before treating any
rule as a constraint.

**Engine rules (apply to us):**
- Slugcat = **6 sprite parts, ~87 vanilla frames** (PlayerArm 15, HeadA 18, LegsA 32, FaceA/B 20, BodyA 1, HipsA 1). **Frame counts are fixed** — DMS can't add frames, and for us they're fixed because `PlayerGraphics` indexes frames positionally in code.
- Sprites are **white because color is applied in code (multiply-style)**. Manually painted colour multiplies against the code colour and goes muddy unless the part is set white.
- **Color intent (corrected):** `00FFFF` is the **UI/icon** color, not the body. Body is **mostly white with a hand-drawn cyan stripe.** To make that show, keep the body part **white** (set `custom_colors` Body ≈ white so the drawn cyan isn't tinted away) and draw the stripe on the sprite. Confirm the exact SlugBase mapping of `color` vs `custom_colors` Body vs UI when testing.
- **Pure black `#000000` renders as transparent** — use `#0E0202` for dark markings. Near-black shades can also go transparent under some room palettes / dark shaders.
- Sprite colours react to **room shaders, not region palettes**, and can shift hue temporarily.
- Idle head/face expected **roughly symmetrical** (they flip when moving); heavy asymmetry makes markings visibly "flash" to the opposite side.
- **Anchors:** expanding a canvas without knowing the anchor location dislocates the sprite from its centre. Expansion must use **even** multipliers. Arms → height only, legs → width only, head/face → both, **body/hips → never** (anchor-sensitive).
- Arm **"Obstruction Point"** at the low bottom of the frames — arms cross when receding, so heavy low detail betrays it. Worse on large arms.

**`HeadA` frame groups** (18 frames — matters for drawing order and for the ears):

| Frames | Group | Used for |
|---|---|---|
| 0–3 | Idle | Standing, facing viewer; 1–3 flash when slightly angled |
| 4–5 | Turning cycle | Vertical-pole sway, side-angle transition, sleeping |
| 6–7 | Crawl turn | Tunnel turning in-betweens |
| 8–17 | Static down | Backflip, peeking from vertical tunnels |

Faces are `FaceA0–A8`, `FaceB0–B8`, plus `FaceDead` and `FaceStunned`; the face auto-flips
with horizontal movement. Blinking can be faked with idle-head frames **1 and 3** (8, 9, 16,
17 for other angles). Face detail can also be drawn onto the 15+ unique `HeadA` frames
directly for smoother turnarounds, at the cost of doing it 15 times.

**⚠ Relevant to the Goblin specifically —** the guide flags as *"perhaps unsolvable"* the
**inconsistent follow-up of the face on heads that are long, have snoots, or are otherwise
unconventionally shaped.** The Goblin's pitch is *"big ol' ears."* Ears drawn into `HeadA`
should be fine (they're part of the head sprite and rotate with it), but anything expected to
track the *face* will drift. Prototype the head early rather than after the body is finished.

**The tail is not a sprite** — it's a `TriangleMesh` with a UV-mapped texture, so none of the
frame rules apply to it:
- Recommended texture **128×64**. Purely white areas take the body colour.
- The texture **deforms and shrinks toward the tip**; small details vanish there.
- Keep markings **short** — spots and horizontal stripes stretch; circles will not stay round.
- The mid-texture band reads as the **underbelly**; it widens hugely at the tip and destroys detail worst there.
- **Longer tails have a higher chance of clipping through terrain** — a real gameplay-visible glitch, not just cosmetic. Argues for a shortish tail on a small character.
- Tail physics can't be rewritten by sprite work (it's procedural).

**Atlas packing** (useful even off-DMS): TexturePacker, Data Format **JSON (Hash)**, border
padding **2**, shape padding **2**. After packing you can edit the `.png` in place; only
dimension/frame changes require a repack. A `.txt` whose dimensions disagree with its `.png`
visually breaks the sprites — the classic failure when reusing a template `.txt` against a
differently-sized sheet.

### Lineage — *Fancy Slugcats* (the original inspiration, now dead)
Doc: <https://docs.google.com/document/d/1cU_kfwB-CI58MtCpuNJT8Pysl5QU88WyJ5gqfWc-HAY/mobilebasic>

FS is the pre-Downpour mod this whole character came from — "a cosmetic tool that allows you
to change various aspects of slugcat's body, such as its width, the proportions of the tail
and ears, as well as the colours," driven by ConfigMachine sliders:

| Group | Parameters |
|---|---|
| Body | height, width, fatness |
| Tail | width, length, roundness |
| Ears | angle, length, width |
| Colours | full RGB |

**DO NOT hunt for the binary.** The doc itself says *"FANCY SLUGCATS IS DISCONTINUED FROM
CURRENT RW VERSION"* and names DMS as its successor. Its dependency stack — ConfigMachine,
CustomSpritesLoader 1.2, PublicityStunt, CustomAssets — is entirely pre-Downpour and partly
discontinued even then, so it cannot load on 1.11.8/Watcher. A copy found in the wild is pure
malware risk for zero function. Sanctus flagged this; agreed and settled.

**What FS tells us that the DMS guide doesn't:**
- **The ears are procedural, not sprite art.** FS drew dynamic ears from a `Circle20` sprite with angle/length/width parameters. They are an appendage drawn in code — like the tail mesh — *not* pixels on `HeadA`. Good: they dodge the "face drifts on unconventional heads" problem. Bad: they're code we'd have to write.
- **Exact frame names** (85 = the 87 above minus the 2 `OnTopOfTerrainHand` frames, which reconciles the two documents):
  - `HeadA0`–`HeadA17`
  - `FaceA0`–`FaceA8`, `FaceB0`–`FaceB8`, `FaceDead`, `FaceStunned`
  - `PlayerArm0`–`PlayerArm12`
  - `BodyA`, `HipsA`
  - `LegsA0`–`LegsA6`, `LegsAAir0`–`LegsAAir1`, `LegsAClimbing0`–`LegsAClimbing6`, `LegsACrawling0`–`LegsACrawling5`, `LegsAOnPole0`–`LegsAOnPole6`, `LegsAPole`, `LegsAVerticalPole`, `LegsAWall`
- **Sprite names are case-sensitive** — be consistent across `.png`, `.txt`, and code.
- "The head sprite will stretch according to the set width" — FS scaled the head rather than swapping art.
- FS's tail work was a *separate* mod (CustomTails), 128×64 recommended — matches the DMS guide exactly.
- `OnTopOfTerrainHandFix.dll` by Henpemaz was a recommended FS addon, aimed at hand placement on terrain. Pre-Downpour and unobtainable, but the *existence* of a dedicated fix says hand anchoring was a known pain point for resized slugcats even then. Expect it to bite us.

### DMS capability audit — RESOLVED 2026-07-19 (read from the installed DLL)
DMS is installed locally; verdict from decompiling
`workshop/content/312520/2948971756/newest/plugins/DressMySlugcat.dll`:

| FS feature | DMS equivalent | Verdict |
|---|---|---|
| Ears (angle/length/width) | `DressMySlugcat.NoirEars` — ears as `TailSegment[][]`, `EarsFlip`, `LastHeadRotation`, atlas `atlases/Ears` | ✅ **has it** |
| Tail (width/length/roundness) | `TailCustomizer` — roundness, custom shape, asym tail, `TailAtlas`/`TailElement` | ✅ **has it** (absorbed CustomTails) |
| Body height / width / fatness | *nothing* — no width/height/fatness strings anywhere; only "Custom Tail Size" | ❌ **does NOT** |

**Decision: the "self-contained, no DMS" call HOLDS**, for a better reason than before.
1. The one thing we actually need — **the smaller body — is the one thing DMS cannot do.** Its
   feature set is sprite replacement plus ears and tail, not proportions.
2. DMS is a **player-facing customization tool**, not a library. Its public surface is
   `FancyMenu`, `GalleryDialog`, `SaveManager`, `DMSOptions`, presets, "Wipe All Presets" —
   there is no evident mod-facing API to ship a fixed appearance for a custom slugcat. Taking
   the dependency would mean *players dress the Goblin themselves*, which is not shipping a
   character. (Not 100% proven — no API was found, but absence of evidence. Re-check if we
   ever seriously want the dependency.)
3. Two mods both hooking `PlayerGraphics.DrawSprites` for the same player is exactly the
   interference the DMS guide warns about.

**But steal the approach.** DMS's ears are the proven design: **ears are `TailSegment` chains**
— the same procedural physics as the tail — driven off head rotation, with their own atlas.
That is how to build the Goblin's ears; FS's `Circle20` was the cruder ancestor. No need to
invent a scheme.

**Sprite-leaser indices** (DMS's own constants — *verify against `PlayerGraphics` before
relying on these*, but they answer the checklist item below):

| Index | Sprite |
|---|---|
| 0 | Body |
| 1 | Hips |
| 2 | Tail |
| 3 | Head |
| 4 | Legs |
| 5 / 6 | Arm, Arm2 |
| 7 / 8 | OnTopOfTerrainArm, OnTopOfTerrainArm2 |

**Hook set DMS uses on `PlayerGraphics`** (a good map of what we'll need): `.ctor`,
`InitiateSprites`, `AddToContainer`, `DrawSprites`, `ApplyPalette`, `Reset`, `Update`.

### Templates — already on disk, no download needed
`PLAN.md` previously said to grab DMS's `ModTemplate.zip`. It's already installed at
`workshop/content/312520/2948971756/dressmyslugcat/` — both `template/` and
`asymmetry template/`. Each part ships `.png` + `.txt`, where the `.txt` is
**TexturePacker JSON (Hash)** despite the extension, e.g.:

```json
{"frames": {
"TailTexture.png": {
	"frame": {"x":2,"y":2,"w":150,"h":75},
	"rotated": false, "trimmed": false,
	"spriteSourceSize": {"x":0,"y":0,"w":150,"h":75},
	"sourceSize": {"w":150,"h":75}
}}, "meta": { ... "image": "tail.png", "format": "RGBA8888", "size": {"w":154,"h":79} }}
```

Template sheet dimensions (2px padding, hence the +4):

| Sheet | Size | Sheet | Size |
|---|---|---|---|
| `head.png` | 1008×240 | `arm.png` | 248×874 |
| `legs.png` | 1762×49 | `face.png` | 242×202 |
| `body.png` | 28×25 | `hips.png` | 32×38 |
| `tail.png` | 154×79 (`TailTexture` 150×75) | | |

Note the tail template is **150×75**, not the 128×64 the guide "recommends" — either works,
it's a UV texture, but match whichever the `.txt` declares.

**Recovering old FS/CustomTails art is viable.** The tail is a plain UV-mapped texture with no
frame data, so an old tail `.png` from a pre-Downpour install is still usable as raw art — only
the `.txt`'s declared dimensions need to agree with it. FS ears were procedural
(`Circle20` + parameters), so there is no "ear file" to recover unless custom ear sprites were
packed.

### DMS-only — does NOT constrain us
- The **asymmetric template system** (Front/Left/Right = 3 `.png` + 3 `.txt` per part, all required or it breaks) is a **DMS feature**, not vanilla. We get one atlas per part.
- `metadata.json` per skin subfolder, the `dressmyslugcat/` folder layout, and author+skin ID naming.
- Menu-reload visual glitches (colours reverting, parts vanishing) — a DMS menu bug.
- "Can't add frames **using only DMS**" — true for us too, but for a different reason (code-side frame indexing).
- Gourmand's forced body stretch, Artificer's scar drift, Spearmaster's detaching tail spots — other slugcats' hardcoded quirks, irrelevant here.

### Integration — self-contained (DECIDED, no DMS)
Not using DMS. Register an `FAtlas` in the plugin, drop `png` + `txt` into `mod/atlases/`, assign via a `PlayerGraphics.DrawSprites` hook (replaces existing parts only). No external dependency, fully bundled — better for co-op robustness, and it's the same hook the limb re-anchoring needs. Still grab the DMS `ModTemplate.zip` for its labeled templates + `.txt` files (saves manual work even off-DMS). Mechanics documented on the "Custom Player Graphics (without DMS)" wiki page.

### Decompile results — DONE 2026-07-19

**Sprite-leaser index map** (read from `PlayerGraphics.InitiateSprites`; DMS's constants
verified correct):

| Index | Sprite | Notes |
|---|---|---|
| 0 | `BodyA` | `anchorY = 0.7894737`; **`if (RenderAsPup) scaleY = 0.5`** |
| 1 | `HipsA` | |
| 2 | Tail | `TriangleMesh`, 13 triangles, `Futile_White` |
| 3 | `HeadA0` / `HeadB0` | |
| 4 | `LegsA0` | has its own `anchorY` |
| 5, 6 | `PlayerArm0` ×2 | `anchorX` set; sprite 5 also gets a `scaleY` |
| 7, 8 | `OnTopOfTerrainHand` ×2 | |
| 9 | `FaceA0` | |
| 10 | `Futile_White` | |
| 11 | `pixel` | |

**How arm sprites get positioned** — this is the mechanism behind "arms out the cheeks":

```csharp
// PlayerGraphics.DrawSprites, per hand i
Vector2 handPos = Vector2.Lerp(hands[i].lastPos, hands[i].pos, timeStacker);
if (hands[i].mode != Limb.Mode.Retracted) {
    sLeaser.sprites[5 + i].x = handPos.x - camPos.x;
    sLeaser.sprites[5 + i].y = handPos.y - camPos.y;
}
```

The arm sprite is drawn **at the simulated hand position**, and is not derived from the body
sprite in any way. `hands` are `SlugcatHand` physics limbs built in the `PlayerGraphics` ctor:

```csharp
hands[i] = new SlugcatHand(this, owner.bodyChunks[0], i, 3f, 0.8f, 1f);
```

— i.e. **anchored to `bodyChunks[0]`**. So shrinking the drawn body moves nothing; the hands
keep resting where a full-size body's chunk says they should. Legs are the same idea via
`legs` (`GenericBodyPart`).

**⭐ The key discovery — three INDEPENDENT knobs.** From `Player..ctor`:

```csharp
float mass = 0.7f * slugcatStats.bodyWeightFac;
bodyChunks[0] = new BodyChunk(this, 0, default, 9f, mass / 2f);   // rad 9
bodyChunks[1] = new BodyChunk(this, 1, default, 8f, mass / 2f);   // rad 8
```

| Knob | Set by | Controls |
|---|---|---|
| **Sprite art / scale** | our atlas + `DrawSprites` | purely what you see |
| **Chunk `rad`** (9 and 8) | **hardcoded — ignores `bodyWeightFac`** | physical size, collision, gap-fitting, **and where limbs anchor** |
| **Chunk mass** | `0.7 × bodyWeightFac` (JSON `weight`) | momentum, and the `HeavyCarry` threshold |

So the Goblin today is **lighter but exactly the same physical size as every other slugcat** —
`weight: 0.8` changed only his mass. Nothing in SlugBase JSON touches `rad`.

**Two viable strategies for "smaller":**
1. **Visual-only** — smaller art + offset `sprites[5..8]` in a `DrawSprites` hook. The game
   itself does something like this: `RenderAsPup` just sets the body sprite's `scaleY = 0.5`.
   Cheap and physics-safe, but the hand *simulation* is untouched, so he'd still reach and grab
   at full-size distances; the offset is cosmetic and can desync from what he can actually touch.
2. **Physical** — hook `Player.ctor` and reduce `rad` from 9/8. Limbs then follow **for free**,
   because they anchor to the chunk. But it changes collision: he'd fit through gaps no other
   slugcat fits through, which is a gameplay change (possibly a *desirable* one for a goblin —
   but it must be a decision, not an accident).

Decide between these before drawing anything, since option 2 changes what "smaller" means for
the art. Prototype cheaply by hooking `Player.ctor` and tweaking `rad` with the *vanilla*
sprite — the distortion will show exactly how far the limbs move.

**Still open:**
- [ ] Decide visual-only vs physical `rad` change (above).
- [ ] `legs` / `GenericBodyPart` — confirm leg anchoring mirrors the hands before assuming it.
- [ ] `FAtlas` registration pattern (wiki page, not the decompile).
- [ ] Check whether `RenderAsPup`'s `scaleY = 0.5` is reusable/overridable for our own scaling.

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
- [x] **Apply the two JSON fixes** — `auto_grab_batflies` is boolean `true`; the ghost scene is removed (below).

**Select-menu scene — REMOVED 2026-07-19, art preserved.** `mod/slugbase/scenes/slugcat_goblin.json`
was deleted: it was orphaned once `select_menu_scene` came out of `the_goblin.json`, and SlugBase
was loading it every launch for nothing. **The art is intentionally kept** at
`mod/scenes/slugcat - goblin/` (Background, Slugcat, Grass 1–3). Revive this verbatim if the
Goblin ever gets a campaign/story — the positions are hand-tuned, so don't redo them:

```json
{
	"id": "Slugcat_Goblin",
	"scene_folder": "scenes/slugcat - goblin",
	"images": [
		{ "name": "Background", "pos": [492, 297], "depth": 3.7, "shader": "Basic" },
		{ "name": "Slugcat",    "pos": [605, 427], "depth": 2.8, "shader": "Basic" },
		{ "name": "Grass 3",    "pos": [602, 264], "depth": 2.2, "shader": "Basic" },
		{ "name": "Grass 2",    "pos": [446, 283], "depth": 2.0, "shader": "Basic" },
		{ "name": "Grass 1",    "pos": [515, 265], "depth": 1.8, "shader": "Basic" }
	],
	"idle_depths": [ 2.8 ],
	"glow_pos": [688, 484],
	"mark_pos": [689, 583],
	"select_menu_pos": [0, 0],
	"slugcat_depth": 2.8
}
```

To re-enable: restore that file and add `"select_menu_scene": "Slugcat_Goblin"` back to
`the_goblin.json`'s features.
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
