using System;
using BepInEx;
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
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // The Goblin is Jolly Co-op-only for now and has no campaign to "beat",
            // so force him unlocked. SlugBase adds custom slugcats to the single-player
            // select carousel automatically, but the Jolly Co-op selector additionally
            // gates on SlugcatStats.SlugcatUnlocked (which SlugBase does NOT hook) — so
            // without this he loads fine yet never appears as a selectable co-op player.
            On.SlugcatStats.SlugcatUnlocked += SlugcatStats_SlugcatUnlocked;

            // Put your custom hooks here!
            //On.Player.Jump += Player_Jump;

        }

        // Report the Goblin as unlocked; defer to the game for every other slugcat.
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