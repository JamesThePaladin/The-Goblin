using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace SlugTemplate
{
    /// <summary>
    /// Lizard-style wall clinging (PLAN.md Feature 4), held on the SPECIAL button.
    ///
    /// The mechanism comes from Lizard.Update, which is trivial: a climbing lizard is simply an
    /// object with gravity switched off whose limbs hold terrain. There is no special climb
    /// physics. What is NOT ported is route choice — lizards pick where to climb via
    /// MovementConnection pathfinding on the room's AI map, and a Player has no AI path.
    ///
    /// IMPORTANT — why we cancel gravity in velocity rather than setting the property:
    ///     PhysicalObject.gravity is  { get => g * room.gravity;  set => g = value; }
    /// and Player.Update assigns it every frame, consuming it inside orig(). So setting
    /// `self.gravity = 0f` from a post-orig hook is overwritten before it can ever apply —
    /// the first version of this file did exactly that and did nothing at all. Adding the
    /// applied gravity back onto velocity is order-independent and leaves no state to reset.
    /// </summary>
    internal static class GoblinWallWalk
    {
        public static readonly PlayerFeature<bool> WallWalk = PlayerBool("thegoblin/wall_walk");

        /// <summary>Frames of continuous contact before he sticks.</summary>
        public static readonly PlayerFeature<float> GripDelay = PlayerFloat("thegoblin/wall_grip_delay");

        /// <summary>
        /// Friction while clinging, applied ONLY to axes the player isn't steering. 1 = none
        /// (skates around on momentum), lower = firmer grip. This is deliberately not a blanket
        /// velocity damp: the first version multiplied ALL velocity every frame, which felt like
        /// wading through glue and quietly destroyed jump height. Ground friction works this way
        /// too — you grip when you aren't pushing, and move freely when you are.
        /// </summary>
        public static readonly PlayerFeature<float> GripFriction = PlayerFloat("thegoblin/wall_grip_friction");

        private const float TileSize = 20f;

        private class GripState
        {
            public int contact;
        }

        private static readonly ConditionalWeakTable<Player, GripState> States =
            new ConditionalWeakTable<Player, GripState>();

        public static void Apply()
        {
            On.Player.Update += Player_Update;
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self?.room == null) return;
            if (!WallWalk.TryGet(self, out bool enabled) || !enabled) return;

            GripDelay.TryGet(self, out float delayF);
            GripFriction.TryGet(self, out float friction);

            int delay = Mathf.Max(1, Mathf.RoundToInt(delayF <= 0f ? 4f : delayF));
            if (friction <= 0f) friction = 0.8f;

            GripState state = States.GetValue(self, _ => new GripState());

            // Held on SPECIAL, the same button the Watcher's ability uses. Nothing happens
            // unless the player asks for it, so normal movement is completely untouched.
            bool wants = self.input != null && self.input.Length > 0 && self.input[0].spec;

            // Hysteresis, mirroring the lizard's inAllowedTerrainCounter / regainFootingCounter.
            // A raw per-frame terrain test chatters at tile boundaries.
            if (wants && HasGrippableTerrain(self))
                state.contact = Mathf.Min(state.contact + 1, delay * 2);
            else
                state.contact = 0;   // release instantly, so letting go feels responsive

            if (state.contact < delay) return;

            // Cancel the gravity that orig() already applied this frame. `self.gravity` is the
            // value Player.Update just set, i.e. exactly what was subtracted from velocity.
            float applied = self.gravity;

            // Friction only on axes with no steering input, so he holds position on the wall
            // instead of sliding, but still accelerates freely when you push. Without this he
            // keeps all horizontal momentum and skates around like he's on ice.
            var inp = self.input[0];
            bool steeringX = inp.x != 0;
            bool steeringY = inp.y != 0;

            foreach (var chunk in self.bodyChunks)
            {
                chunk.vel.y += applied;

                if (friction < 1f)
                {
                    if (!steeringX) chunk.vel.x *= friction;
                    if (!steeringY) chunk.vel.y *= friction;
                }
            }
        }

        /// <summary>
        /// True only where there is something real to hold — the guard against hanging in
        /// mid-air. Requires either a background wall on the occupied tile (Tile.wallbehind,
        /// the "climbable background piece"), or a Solid/Slope surface in one of the four
        /// neighbouring tiles. Plain air with nothing behind it is deliberately rejected.
        /// </summary>
        private static bool HasGrippableTerrain(Player self)
        {
            Room room = self.room;

            foreach (var chunk in self.bodyChunks)
            {
                Vector2 pos = chunk.pos;

                if (room.GetTile(pos).wallbehind)
                    return true;

                if (IsSurface(room, pos + new Vector2(TileSize, 0f)) ||
                    IsSurface(room, pos + new Vector2(-TileSize, 0f)) ||
                    IsSurface(room, pos + new Vector2(0f, TileSize)) ||
                    IsSurface(room, pos + new Vector2(0f, -TileSize)))
                    return true;
            }

            return false;
        }

        private static bool IsSurface(Room room, Vector2 worldPos)
        {
            var terrain = room.GetTile(worldPos).Terrain;
            return terrain == Room.Tile.TerrainType.Solid
                || terrain == Room.Tile.TerrainType.Slope;
        }
    }
}
