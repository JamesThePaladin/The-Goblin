using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "The Goblin", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "sanctus.thegoblin";

        // The Goblin's SlugcatStats.Name — SlugBase registers this from the JSON "id".
        // ExtEnum identity is the id string, so we match on that.
        public const string GoblinName = "Goblin";

        //public static readonly PlayerFeature<bool> ExplodeOnDeath = PlayerBool("thegoblin/explode_on_death");
        //public static readonly GameFeature<float> MeanLizards = GameFloat("thegoblin/mean_lizards");

        // EXPERIMENT (PLAN.md Feature 3): uniform scale on the Goblin's body chunks.
        // Lives in the_goblin.json so it can be retuned WITHOUT a rebuild — SlugBase
        // hot-reloads character JSON, and the value is read fresh in Player.ctor, so a
        // death/respawn picks up an edit. 1.0 = vanilla, no-op.
        public static readonly PlayerFeature<float> BodyScale = PlayerFloat("thegoblin/body_scale");


        // Add hooks
        public void OnEnable()
        {
            _log = Logger;

            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // The Goblin is Jolly Co-op-only for now and has no campaign to "beat",
            // so force him unlocked. SlugBase adds custom slugcats to the single-player
            // select carousel automatically, but the Jolly Co-op selector additionally
            // gates on SlugcatStats.SlugcatUnlocked (which SlugBase does NOT hook) — so
            // without this he loads fine yet never appears as a selectable co-op player.
            On.SlugcatStats.SlugcatUnlocked += SlugcatStats_SlugcatUnlocked;

            // Movement: borrow Rivulet's whole land agility kit (see PLAN.md Feature 1b).
            HookRivuletAgility();

            // Wall-walking, step 1: gravity + terrain gating (PLAN.md Feature 4).
            GoblinWallWalk.Apply();

            // Custom tail texture (PLAN.md Feature 3).
            GoblinTail.Apply();

            // Hoarding: don't let being small stop him carrying loot (PLAN.md Feature 1c).
            On.Player.HeavyCarry += Player_HeavyCarry;

            // Body scale + the weight workaround (PLAN.md Feature 3).
            On.Player.ctor += Player_ctor_BodyScale;
        }

        // Set from OnEnable so the static hook helpers can log. BepInEx writes to
        // BepInEx/LogOutput.log (verified working once Doorstop injects — see PLAN.md).
        private static ManualLogSource _log;

        private static bool IsGoblin(Player player) =>
            player?.SlugCatClass != null && player.SlugCatClass.value == GoblinName;

        // Every one of Rivulet's ~40 movement branches — pole-skip/beam vaulting in Update,
        // the slide timing in UpdateBodyMode, WallJump, Jump, and the matching animations —
        // gates on the single `Player.isRivulet` property. Reporting true for the Goblin
        // grants the entire kit at once, with her exact constants and no hand-tuning.
        //
        // This is the same mechanism the base game uses: Expedition's "unl-agility" unlock
        // is literally the property's second branch, so it is built to be slugcat-agnostic.
        //
        // Audited for water leakage before adopting — her swimming power (swimBoostCost,
        // swimBoostCooldown, lungsFac) lives in SlugcatStats keyed on slugcat NAME, so none
        // of it can transfer. The only non-land branches are a drowning threshold in
        // LungUpdate (a mild downside) and being able to grab while submerged on a beam
        // (a bonus that suits a hoarder). Neither is worth suppressing.
        //
        // MonoMod's HookGen emits no delegates for property getters (7214 orig_* in
        // HOOKS-Assembly-CSharp, zero named get_*), so there is no On.Player.get_isRivulet
        // to subscribe to — the getter has to be detoured through RuntimeDetour instead.
        private static void HookRivuletAgility()
        {
            MethodInfo getter = typeof(Player)
                .GetProperty("isRivulet", BindingFlags.Public | BindingFlags.Instance)
                ?.GetGetMethod();

            if (getter == null)
            {
                _log?.LogError("Player.isRivulet getter not found — the Goblin's movement kit is OFF.");
                return;
            }

            // Held in a static field so the detour isn't garbage collected.
            _isRivuletHook = new Hook(getter, (Func<Func<Player, bool>, Player, bool>)Player_get_isRivulet);
            _log?.LogInfo("Detoured Player.isRivulet — Goblin has Rivulet's agility kit.");
        }

        private static Hook _isRivuletHook;

        private static bool Player_get_isRivulet(Func<Player, bool> orig, Player self)
        {
            // Per-player gate: only the Goblin is forced true, everyone else (including
            // the actual Rivulet in the same co-op session) falls through untouched.
            if (IsGoblin(self))
                return true;

            return orig(self);
        }

        // HeavyCarry's mass rule is relative to the carrier, so the Goblin's light `weight`
        // makes MORE objects count as heavy for him than for Survivor. Worse, FreeHand()
        // returns -1 while heavy-carrying, meaning he couldn't hold a second object at all —
        // the opposite of a hoarder. Exempt him from the mass rule only; genuinely two-handed
        // or draggable things (and other players) stay heavy so their animations still work.
        private static bool Player_HeavyCarry(On.Player.orig_HeavyCarry orig, Player self, PhysicalObject obj)
        {
            if (obj == null || !IsGoblin(self))
                return orig(self, obj);

            if (obj is Player)
                return orig(self, obj);

            if (self.Grabability(obj) >= Player.ObjectGrabability.TwoHands)
                return orig(self, obj);

            return false;
        }

        // ------------------------------------------------------------------
        // EXPERIMENT: body scale. Throwaway — delete once we've decided between
        // "visual-only" and "physical rad change" (PLAN.md Feature 3).
        // ------------------------------------------------------------------

        // Vanilla player geometry, from Player..ctor:
        //     bodyChunks[0] = rad 9      (hands anchor to this one)
        //     bodyChunks[1] = rad 8
        //     connection distance 17     (== 9 + 8, so the chunks sit exactly touching)
        // Scaling all three by the same factor shrinks him without distorting his
        // proportions. Chunk mass is deliberately NOT touched — that's the separate
        // `weight` knob, and conflating the two would muddy what we're measuring.
        private static void Player_ctor_BodyScale(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);

            if (!BodyScale.TryGet(self, out float scale) || scale <= 0f || Math.Abs(scale - 1f) < 0.001f)
                return;

            foreach (var chunk in self.bodyChunks)
                chunk.rad *= scale;

            foreach (var connection in self.bodyChunkConnections)
                connection.distance *= scale;

            // Also dump the stats object the ctor actually saw. TotalMass came back as the
            // vanilla 0.7 despite "weight": [0.8, 0.6], which means bodyWeightFac was 1.0 here.
            // runspeedFac is the tell: 1.75 means these ARE the Goblin's stats and only weight
            // is misbehaving; 1.0 means Player.slugcatStats resolved to the wrong object
            // entirely (it reads session.characterStatsJollyplayer[n] when populated and falls
            // back to the session-wide characterStats otherwise) — which would be co-op-critical.
            var stats = self.slugcatStats;

            // `weight` is not landing: bodyWeightFac reads 1.0 despite "weight": [0.8, 0.6],
            // while walk_speed from the same JSON applies fine. SlugBase registers it as
            // PlayerFloats("weight", 1, 2) — identical in shape to the speed features — and
            // sets bodyWeightFac = ApplyStarve(weight, Mathf.Min(weight[0], 0.9f), ...), which
            // should give 0.8. So the suspicion is TryGet returning false. Ask it directly.
            ApplyWeightWorkaround(self);
            _log?.LogInfo($"[bodyscale] applied {scale:0.###} -> rad0={self.bodyChunks[0].rad:0.##} " +
                          $"rad1={self.bodyChunks[1].rad:0.##} dist={self.bodyChunkConnections[0].distance:0.##} " +
                          $"mass={self.TotalMass:0.###} (vanilla: 9 / 8 / 17) | " +
                          $"stats: bodyWeightFac={stats?.bodyWeightFac ?? -1f:0.###} " +
                          $"runspeedFac={stats?.runspeedFac ?? -1f:0.###} " +
                          $"poleClimb={stats?.poleClimbSpeedFac ?? -1f:0.###} " +
                          $"corridorClimb={stats?.corridorClimbSpeedFac ?? -1f:0.###} " +
                          $"loudness={stats?.loudnessFac ?? -1f:0.###} " +
                          $"lungs={stats?.lungsFac ?? -1f:0.###} " +
                          $"player#{self.playerState?.playerNumber ?? -1}");
        }

        // WORKAROUND, not a root-cause fix. SlugBase's `weight` is the only feature that does
        // not reach the Goblin's SlugcatStats: measured at Player..ctor, walk/climb/tunnel/
        // loudness/lungs all apply correctly, but bodyWeightFac stays vanilla 1.0. SlugBase
        // reads the value fine (WeightMul.TryGet=True, values=0.4/0.2) and its ApplyStarve
        // returns values[0] for a non-malnourished slugcat, so it should be writing 0.4.
        // Why it doesn't is still unexplained — see PLAN.md. We set it ourselves.
        //
        // Set bodyWeightFac, NOT the chunk masses: Player.SetMalnourished recomputes
        //     bodyChunks[i].mass = 0.7f * slugcatStats.bodyWeightFac / 2f
        // whenever starve state changes, so a raw mass write would be silently reverted.
        // We mirror that same formula to fix up the masses the constructor just set.
        private static void ApplyWeightWorkaround(Player self)
        {
            if (!PlayerFeatures.WeightMul.TryGet(self, out float[] weights) || weights == null || weights.Length == 0)
                return;

            var stats = self.slugcatStats;
            if (stats == null)
                return;

            float wanted = (stats.malnourished && weights.Length > 1) ? weights[1] : weights[0];
            if (Math.Abs(stats.bodyWeightFac - wanted) < 0.001f)
                return;   // SlugBase got there after all — leave it alone.

            float before = stats.bodyWeightFac;
            stats.bodyWeightFac = wanted;

            float mass = 0.7f * wanted;
            foreach (var chunk in self.bodyChunks)
                chunk.mass = mass / 2f;

            _log?.LogInfo($"[weightfix] bodyWeightFac {before:0.###} -> {wanted:0.###} " +
                          $"(malnourished={stats.malnourished}); TotalMass now {self.TotalMass:0.###}");
        }



        // Report the Goblin as unlocked; defer to the game for every other slugcat.
        // NOTE: this runs very hot (many calls per menu frame) — keep it allocation-free
        // and never log from here.
        private static bool SlugcatStats_SlugcatUnlocked(On.SlugcatStats.orig_SlugcatUnlocked orig, SlugcatStats.Name i, RainWorld rainWorld)
        {
            if (i != null && i.value == GoblinName)
                return true;

            return orig(i, rainWorld);
        }

        //Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
            // Runs at OnModsInit, once the game's file system and Futile are ready.
            GoblinTail.LoadTexture(_log);
        }

        //// Implement MeanLizards
        //private void Lizard_ctor(On.Lizard.orig_ctor orig, Lizard self, AbstractCreature abstractCreature, World world)
        //{
        //    orig(self, abstractCreature, world);

        //    if(MeanLizards.TryGet(world.game, out float meanness))
        //    {
        //        self.spawnDataEvil = Mathf.Min(self.spawnDataEvil, meanness);
        //    }
        //}


        //// Implement SuperJump
        //private void Player_Jump(On.Player.orig_Jump orig, Player self)
        //{
        //    orig(self);

        //    if (SuperJump.TryGet(self, out var power))
        //    {
        //        self.jumpBoost *= 1f + power;
        //    }
        //}

        //// Implement ExlodeOnDeath
        //private void Player_Die(On.Player.orig_Die orig, Player self)
        //{
        //    bool wasDead = self.dead;

        //    orig(self);

        //    if(!wasDead && self.dead
        //        && ExplodeOnDeath.TryGet(self, out bool explode)
        //        && explode)
        //    {
        //        // Adapted from ScavengerBomb.Explode
        //        var room = self.room;
        //        var pos = self.mainBodyChunk.pos;
        //        var color = self.ShortCutColor();
        //        room.AddObject(new Explosion(room, self, pos, 7, 250f, 6.2f, 2f, 280f, 0.25f, self, 0.7f, 160f, 1f));
        //        room.AddObject(new Explosion.ExplosionLight(pos, 280f, 1f, 7, color));
        //        room.AddObject(new Explosion.ExplosionLight(pos, 230f, 1f, 3, new Color(1f, 1f, 1f)));
        //        room.AddObject(new ExplosionSpikes(room, pos, 14, 30f, 9f, 7f, 170f, color));
        //        room.AddObject(new ShockWave(pos, 330f, 0.045f, 5, false));

        //        room.ScreenMovement(pos, default, 1.3f);
        //        room.PlaySound(SoundID.Bomb_Explode, pos);
        //        room.InGameNoise(new Noise.InGameNoise(pos, 9000f, self, 1f));
        //    }
        //}
    }
}