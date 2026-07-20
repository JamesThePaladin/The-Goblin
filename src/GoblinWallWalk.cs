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

        /// <summary>How quickly he reaches crawl speed. 1 = instant, lower = softer start.</summary>
        public static readonly PlayerFeature<float> CrawlAccel = PlayerFloat("thegoblin/wall_crawl_accel");

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

            public bool clinging;
            public Vector2 lookDir = new Vector2(0f, 1f);
            public Vector2 move;
            public float reach = 22f;
        }

        private static readonly ConditionalWeakTable<Player, GripState> States =
            new ConditionalWeakTable<Player, GripState>();

        public static void Apply()
        {
            On.Player.Update += Player_Update;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
        }

        /// <summary>
        /// Step 3: stop him staring at the camera while crawling.
        ///
        /// `lookDirection` is what shifts the face sprite within the head —
        /// `Lerp(lastLookDir, lookDirection, t) * 3f`, with Mathf.Sign(x) flipping it. Pointing
        /// it along the crawl direction turns his face where he's going instead of at the
        /// viewer, which is what made wall-crawling look wrong.
        ///
        /// Runs on PlayerGraphics.Update, after Player.Update has refreshed the grip state.
        /// </summary>
        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);

            if (self?.player == null) return;
            if (!States.TryGetValue(self.player, out GripState state) || !state.clinging) return;

            self.lookDirection = state.lookDir;

            // Hand holds are applied HERE, not from Player.Update. PlayerGraphics.Update runs
            // afterwards and vanilla reassigns the hand modes every frame, so grips set earlier
            // were being overwritten — which is why he let go the moment he stopped moving.
            UpdateHandHolds(self.player, state, state.move, state.reach);
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self?.room == null) return;
            if (!WallWalk.TryGet(self, out bool enabled) || !enabled) return;

            GripDelay.TryGet(self, out float delayF);
            CrawlAccel.TryGet(self, out float accel);
            CrawlSpeed.TryGet(self, out float crawl);
            GripReach.TryGet(self, out float reach);

            int delay = Mathf.Max(1, Mathf.RoundToInt(delayF <= 0f ? 4f : delayF));
            if (accel <= 0f) accel = 0.35f;
            accel = Mathf.Clamp01(accel);
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
                state.clinging = false;
                ReleaseHands(self, state);
                return;
            }

            state.clinging = true;

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

            // Velocity TARGET, not an impulse. Earlier versions did `vel += move * crawl` every
            // frame with damping only on unsteered axes, so the steered axis accumulated without
            // limit — that's why it ran at roughly double his normal pace. Steering toward a
            // target means `crawl` IS the top speed, in units per frame.
            //
            // It also fixes the drift. The gravity cancellation above leaves a tiny residual
            // (Player.Update reassigns g at a different point in the frame than when
            // BodyChunk.Update consumed it via `vel.y -= owner.gravity`). Previously that
            // residual accumulated — downward as a slow slide, then upward as a constant climb
            // once the slide was clamped. An axis with no input is now pinned to zero outright,
            // so nothing can integrate: a grip simply does not slip, in either direction.
            Vector2 target = move * crawl;

            foreach (var chunk in self.bodyChunks)
            {
                chunk.vel.x = steeringX ? Mathf.Lerp(chunk.vel.x, target.x, accel) : 0f;

                // VERTICAL IS KINEMATIC — we set the displacement outright rather than trying to
                // balance forces. Two earlier attempts failed because `self.gravity` read after
                // orig() is NOT the value BodyChunk.Update actually applied, so both cancelling
                // it and compensating for it were no-ops. That is also why only pinning position
                // ever stopped the slide. Rather than keep guessing at the force balance, own
                // the axis: while clinging he moves exactly `target.y` per frame vertically, or
                // exactly nothing. Deterministic, and it makes up/down match left/right, which
                // it previously didn't because gravity was silently eating part of the climb.
                float dy = steeringY ? target.y : 0f;
                chunk.pos.y = chunk.lastPos.y + dy;
                chunk.vel.y = dy;
            }

            // Face where he's crawling. Hold the last direction when idle rather than snapping
            // back, so pausing mid-climb doesn't make him glance at the camera.
            if (move.sqrMagnitude > 0.01f)
                state.lookDir = move;

            state.move = move;
            state.reach = reach;
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
                    // Driving mode/absoluteHuntPos does nothing: SlugcatHand.Update reassigns
                    // both every frame AND moves the limb toward its own target in the same
                    // pass, so anything we set post-orig is never read before being overwritten.
                    // Set the resulting position directly instead. Lerping rather than snapping
                    // keeps a visible reach when a hand moves to a new hold.
                    hand.mode = Limb.Mode.HuntAbsolutePosition;
                    hand.absoluteHuntPos = state.holds[i];

                    hand.pos = Vector2.Lerp(hand.pos, state.holds[i], 0.4f);
                    hand.vel *= 0.5f;
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
