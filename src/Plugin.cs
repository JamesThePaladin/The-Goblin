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

        //public static readonly PlayerFeature<float> SuperJump = PlayerFloat("thegoblin/super_jump");
        //public static readonly PlayerFeature<bool> ExplodeOnDeath = PlayerBool("thegoblin/explode_on_death");
        //public static readonly GameFeature<float> MeanLizards = GameFloat("thegoblin/mean_lizards");


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

            // Hoarding: don't let being small stop him carrying loot (PLAN.md Feature 1c).
            On.Player.HeavyCarry += Player_HeavyCarry;
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