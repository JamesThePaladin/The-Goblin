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

        /// <summary>Crawl acceleration while clinging. This is a crawl, not a run.</summary>
        public static readonly PlayerFeature<float> CrawlSpeed = PlayerFloat("thegoblin/wall_crawl_speed");

        /// <summary>How far a hand's hold can trail the body before it reaches for a new one.</summary>
        public static readonly PlayerFeature<float> GripReach = PlayerFloat("thegoblin/wall_grip_reach");

        private const float TileSize = 20f;

        private class GripState
        {
            public int contact;

            // Where each hand is currently holding, in world space. Hands hold a fixed point
            // while the body moves past it, then reach for a new one — that alternation is what
            // reads as crawling rather than sliding.
            public Vector2[] holds = new Vector2[2];
            public bool[] holding = new bool[2];
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
            CrawlSpeed.TryGet(self, out float crawl);
            GripReach.TryGet(self, out float reach);

            int delay = Mathf.Max(1, Mathf.RoundToInt(delayF <= 0f ? 4f : delayF));
            if (friction <= 0f) friction = 0.8f;
            if (crawl <= 0f) crawl = 1.2f;
            if (reach <= 0f) reach = 22f;

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

            if (state.contact < delay)
            {
                ReleaseHands(self, state);
                return;
            }

            // Cancel the gravity that orig() already applied this frame. `self.gravity` is the
            // value Player.Update just set, i.e. exactly what was subtracted from velocity.
            float applied = self.gravity;

            // Friction only on axes with no steering input, so he holds position on the wall
            // instead of sliding, but still accelerates freely when you push. Without this he
            // keeps all horizontal momentum and skates around like he's on ice.
            var inp = self.input[0];
            bool steeringX = inp.x != 0;
            bool steeringY = inp.y != 0;

            // Crawl on BOTH axes. Clinging happens on the background plane, so there is no
            // surface tangent to project onto — up/down/left/right are simply free, which is
            // what lets him climb rather than only shuffle sideways.
            Vector2 move = new Vector2(inp.x, inp.y);
            if (move.sqrMagnitude > 0.01f) move = move.normalized;

            foreach (var chunk in self.bodyChunks)
            {
                chunk.vel.y += applied;

                if (friction < 1f)
                {
                    if (!steeringX) chunk.vel.x *= friction;
                    if (!steeringY) chunk.vel.y *= friction;
                }

                chunk.vel += move * crawl;
            }

            UpdateHandHolds(self, state, move, reach);
        }

        /// <summary>
        /// Drives the hands to grasp fixed points, the way LizardLimb holds a grabPos while the
        /// body moves past it. Each hand keeps its hold until the body has trailed too far, then
        /// reaches for a new point ahead. Only one hand re-grips at a time, so there is always a
        /// hand planted — that alternation is what makes it read as crawling rather than sliding.
        /// </summary>
        private static void UpdateHandHolds(Player self, GripState state, Vector2 move, float reach)
        {
            if (!(self.graphicsModule is PlayerGraphics graphics) || graphics.hands == null) return;

            Vector2 body = self.bodyChunks[0].pos;

            // Perpendicular to travel, so the two hands straddle the direction of movement.
            Vector2 dir = move.sqrMagnitude > 0.01f ? move : new Vector2(0f, 1f);
            Vector2 side = new Vector2(-dir.y, dir.x);

            bool someoneReaching = false;

            for (int i = 0; i < graphics.hands.Length && i < 2; i++)
            {
                var hand = graphics.hands[i];
                if (hand == null) continue;

                float sign = i == 0 ? -1f : 1f;
                bool tooFar = !state.holding[i] || Vector2.Distance(body, state.holds[i]) > reach;

                // Stagger: never let both hands leave the wall in the same frame.
                if (tooFar && !someoneReaching)
                {
                    Vector2 target = body + dir * (reach * 0.5f) + side * (sign * 8f);
                    if (self.room.GetTile(target).Terrain != Room.Tile.TerrainType.Solid)
                    {
                        state.holds[i] = self.room.MiddleOfTile(target);
                        state.holding[i] = true;
                        someoneReaching = true;
                    }
                }

                if (state.holding[i])
                {
                    hand.mode = Limb.Mode.HuntAbsolutePosition;
                    hand.absoluteHuntPos = state.holds[i];
                }
            }
        }

        /// <summary>Hands go back to normal control the moment he stops clinging.</summary>
        private static void ReleaseHands(Player self, GripState state)
        {
            state.holding[0] = false;
            state.holding[1] = false;
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
