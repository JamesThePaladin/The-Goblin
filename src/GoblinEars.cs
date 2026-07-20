using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace SlugTemplate
{
    /// <summary>
    /// Procedural fennec ears for the Goblin.
    ///
    /// Deliberately NOT built on TailSegment, despite that being how DMS does it:
    ///   - TailSegment has no rest-angle concept. It is a dangling chain (spacing + gravity),
    ///     so holding a firm downward angle would mean overriding it every frame.
    ///   - Its update path does terrain collision, which we explicitly do not want.
    /// A small spring chain with a rest direction does exactly what's wanted and is no bigger.
    ///
    /// Every value is a SlugBase feature, so it lives in the_goblin.json and retunes without a
    /// rebuild (edit, respawn, look). See PLAN.md Feature 3.
    /// </summary>
    internal static class GoblinEars
    {
        // Angle is measured off the head's own axis (body -> head):
        //   0   = straight up along the head
        //   90  = straight out sideways
        //   130 = 40 degrees BELOW horizontal  <- Sanctus's "roughly 40 degrees downward"
        public static readonly PlayerFeature<float> EarAngle = PlayerFloat("thegoblin/ear_angle");
        public static readonly PlayerFeature<float> EarLength = PlayerFloat("thegoblin/ear_length");
        public static readonly PlayerFeature<float> EarWidth = PlayerFloat("thegoblin/ear_width");
        public static readonly PlayerFeature<float> EarSegments = PlayerFloat("thegoblin/ear_segments");
        public static readonly PlayerFeature<float> EarStiffness = PlayerFloat("thegoblin/ear_stiffness");
        public static readonly PlayerFeature<float> EarDroop = PlayerFloat("thegoblin/ear_droop");

        // Extra degrees of bend added per segment, so the ear arches instead of running
        // straight. Sign flips the direction of the curve.
        public static readonly PlayerFeature<float> EarCurve = PlayerFloat("thegoblin/ear_curve");

        // Tip profile: 0 = linear taper (sharp triangle), 1 = elliptical (rounded point).
        public static readonly PlayerFeature<float> EarTip = PlayerFloat("thegoblin/ear_tip");

        // How quickly the ears' orientation tracks the head. The head body-part is physics
        // simulated and jitters while moving, so following it raw makes the ears whip around.
        // Low = steady, high = snappy. 0.15 is a good starting point.
        public static readonly PlayerFeature<float> EarFollow = PlayerFloat("thegoblin/ear_follow");

        private const int EarCount = 2;

        private class Ear
        {
            public Vector2[] pos;
            public Vector2[] lastPos;
            public Vector2[] vel;
            public int side;        // -1 left, +1 right
        }

        private class EarData
        {
            public Ear[] ears;
            public int points;
            public float angle, length, width, stiffness, droop, curve, tip, follow;

            // Smoothed head direction. The raw body->head vector is physics-driven and jitters
            // hard while moving, which whipped the ears around. DMS caches LastHeadRotation for
            // this same reason.
            public Vector2 smoothedDir = new Vector2(0f, 1f);
        }

        // Sprite indices are per-SPRITE-LEASER, not per-PlayerGraphics. A single player can
        // have several leasers alive (one per camera, and a fresh one every time the room
        // rebuilds -- e.g. SpitOutOfShortCut when leaving a shelter). Storing the index on the
        // graphics object meant a new leaser reset the array to its base length while the old
        // index still pointed past the end: IndexOutOfRangeException on room transition.
        //
        // It also can't be predicted, because DressMySlugcat is in the same hook chain and adds
        // its own sprites. Always look it up, always bounds-check.
        private class LeaserData
        {
            public int firstSprite = -1;
        }

        private static readonly ConditionalWeakTable<PlayerGraphics, EarData> Data =
            new ConditionalWeakTable<PlayerGraphics, EarData>();

        private static readonly ConditionalWeakTable<RoomCamera.SpriteLeaser, LeaserData> Leasers =
            new ConditionalWeakTable<RoomCamera.SpriteLeaser, LeaserData>();

        /// <summary>Resolves this leaser's ear sprite range, or false if it isn't usable.</summary>
        private static bool TryGetEarSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser,
            out EarData data, out int first)
        {
            first = -1;
            if (!TryGetConfig(self, out data)) return false;
            if (sLeaser?.sprites == null) return false;
            if (!Leasers.TryGetValue(sLeaser, out LeaserData leaser)) return false;

            first = leaser.firstSprite;
            return first >= 0 && first + EarCount <= sLeaser.sprites.Length;
        }

        // Local vector helpers. RWCustom.Custom has equivalents, but its overloads pull in
        // Unity.Mathematics (float2), which isn't in lib/ — not worth a new reference for
        // three lines of trigonometry.
        private static Vector2 DirVec(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float m = d.magnitude;
            return m < 0.0001f ? new Vector2(0f, 1f) : d / m;
        }

        private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static void Apply()
        {
            On.PlayerGraphics.ctor += PlayerGraphics_ctor;
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
        }

        private static bool TryGetConfig(PlayerGraphics self, out EarData data)
        {
            data = null;
            if (self?.player == null) return false;
            return Data.TryGetValue(self, out data) && data.ears != null;
        }

        private static void PlayerGraphics_ctor(On.PlayerGraphics.orig_ctor orig, PlayerGraphics self, PhysicalObject ow)
        {
            orig(self, ow);

            var player = ow as Player;
            if (player == null || !EarLength.TryGet(player, out float length))
                return;

            EarAngle.TryGet(player, out float angle);
            EarWidth.TryGet(player, out float width);
            EarStiffness.TryGet(player, out float stiffness);
            EarDroop.TryGet(player, out float droop);
            EarSegments.TryGet(player, out float segments);
            EarCurve.TryGet(player, out float curve);
            EarTip.TryGet(player, out float tip);
            EarFollow.TryGet(player, out float follow);

            int points = Mathf.Clamp(Mathf.RoundToInt(segments), 2, 12);

            var data = new EarData
            {
                points = points,
                angle = angle,
                length = Mathf.Max(length, 1f),
                width = Mathf.Max(width, 0.5f),
                // 0 = floppy, 1 = rigid. Clamped below 1 so there's always some lag to see.
                stiffness = Mathf.Clamp(stiffness, 0f, 0.95f),
                droop = droop,
                curve = curve,
                tip = Mathf.Clamp01(tip),
                // 0 would freeze the ears' orientation entirely; 1 tracks the raw jitter.
                follow = Mathf.Clamp(follow <= 0f ? 0.15f : follow, 0.01f, 1f),
                ears = new Ear[EarCount],
            };

            for (int e = 0; e < EarCount; e++)
            {
                data.ears[e] = new Ear
                {
                    side = e == 0 ? -1 : 1,
                    pos = new Vector2[points],
                    lastPos = new Vector2[points],
                    vel = new Vector2[points],
                };
            }

            Data.Add(self, data);
        }

        /// <summary>Where an ear's base sits, and which way it rests, in world space.</summary>
        private static void EarFrame(PlayerGraphics self, EarData data, Ear ear, out Vector2 basePos, out Vector2 restDir)
        {
            Vector2 headPos = self.head.pos;
            Vector2 bodyPos = self.player.bodyChunks[0].pos;

            // Head "rotation" in this codebase is a direction vector, not an angle. Use the
            // SMOOTHED one: the raw vector comes from a physics body part that bobs and lags,
            // and feeding it straight in made the ears swing wildly whenever he moved.
            Vector2 headDir = data.smoothedDir;
            if (headDir.sqrMagnitude < 0.0001f) headDir = new Vector2(0f, 1f);

            Vector2 perp = Perp(headDir);

            basePos = headPos + perp * (self.head.rad * 0.5f * ear.side);
            restDir = Rotate(headDir, data.angle * ear.side);
        }

        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);

            if (!TryGetConfig(self, out EarData data)) return;

            float segLen = data.length / (data.points - 1);

            // One smoothing step per frame, shared by both ears.
            Vector2 rawDir = DirVec(self.player.bodyChunks[0].pos, self.head.pos);
            data.smoothedDir = Vector2.Lerp(data.smoothedDir, rawDir, data.follow);
            if (data.smoothedDir.sqrMagnitude > 0.0001f)
                data.smoothedDir = data.smoothedDir.normalized;

            foreach (var ear in data.ears)
            {
                EarFrame(self, data, ear, out Vector2 basePos, out Vector2 restDir);

                // Point 0 is pinned to the head; everything else springs toward the rest pose
                // while carrying its own momentum. That combination is what makes the ears hold
                // a deliberate angle yet still sweep and bounce when he moves.
                ear.lastPos[0] = ear.pos[0];
                ear.pos[0] = basePos;
                ear.vel[0] = Vector2.zero;

                for (int i = 1; i < data.points; i++)
                {
                    ear.lastPos[i] = ear.pos[i];

                    // Each segment's rest direction is rotated a little further than the last,
                    // so the chain arches instead of running dead straight. Mirrored by side so
                    // both ears curve the same way relative to the head.
                    Vector2 segDir = Rotate(restDir, data.curve * i * ear.side);
                    Vector2 rest = ear.pos[i - 1] + segDir * segLen;

                    ear.vel[i] += (rest - ear.pos[i]) * data.stiffness;
                    ear.vel[i] += new Vector2(0f, -data.droop);
                    ear.vel[i] *= 0.8f;                       // damping, or they oscillate forever
                    ear.pos[i] += ear.vel[i];

                    // Hard length constraint keeps the ear from stretching under fast movement.
                    Vector2 fromPrev = ear.pos[i] - ear.pos[i - 1];
                    if (fromPrev.magnitude > segLen)
                        ear.pos[i] = ear.pos[i - 1] + fromPrev.normalized * segLen;
                }
            }
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);

            if (!TryGetConfig(self, out EarData data)) return;

            // APPEND past the end -- never insert. InitiateSprites derives named indices from
            // the array length (gownIndex = count - 1), so anything inserted mid-array silently
            // corrupts them. Recorded against THIS leaser, since the base length varies by
            // slugcat and by what other mods (DMS) appended before us.
            int first = sLeaser.sprites.Length;
            Array.Resize(ref sLeaser.sprites, first + EarCount);

            for (int e = 0; e < EarCount; e++)
                sLeaser.sprites[first + e] = new TriangleMesh("Futile_White", BuildStrip(data.points), false, false);

            Leasers.Remove(sLeaser);
            Leasers.Add(sLeaser, new LeaserData { firstSprite = first });

            // SpriteLeaser..ctor calls InitiateSprites then ApplyPalette and NEVER
            // AddToContainer, so nothing else will parent these for us — without this the ears
            // exist but are attached to no container and never render. Add them directly rather
            // than calling self.AddToContainer, which would re-enter the hook chain (through
            // DMS) mid-construction; that re-entrancy is what caused the shelter crash.
            AttachEars(sLeaser, first, rCam?.ReturnFContainer("Midground"));
        }

        /// <summary>Parents the ear meshes, sitting them just behind the head sprite.</summary>
        private static void AttachEars(RoomCamera.SpriteLeaser sLeaser, int first, FContainer container)
        {
            if (container == null || sLeaser?.sprites == null) return;
            if (first < 0 || first + EarCount > sLeaser.sprites.Length) return;

            // Head is index 3 in vanilla, but never assume the array is that long.
            FSprite head = sLeaser.sprites.Length > 3 ? sLeaser.sprites[3] : null;

            for (int e = 0; e < EarCount; e++)
            {
                FSprite ear = sLeaser.sprites[first + e];
                if (ear == null) continue;

                // AddChild/AddChildAtIndex re-parents safely if it already has a container.
                int headIndex = head != null ? container.GetChildIndex(head) : -1;
                if (headIndex < 0)
                    container.AddChild(ear);
                else
                    container.AddChildAtIndex(ear, headIndex);
            }
        }

        /// <summary>Quad strip: two vertices per point, tapering to a point at the tip.</summary>
        private static TriangleMesh.Triangle[] BuildStrip(int points)
        {
            var tris = new List<TriangleMesh.Triangle>();
            for (int i = 0; i < points - 1; i++)
            {
                int v = i * 2;
                tris.Add(new TriangleMesh.Triangle(v, v + 1, v + 2));
                tris.Add(new TriangleMesh.Triangle(v + 1, v + 2, v + 3));
            }
            return tris.ToArray();
        }

        private static void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
        {
            orig(self, sLeaser, rCam, newContainer);

            if (!TryGetEarSprites(self, sLeaser, out EarData data, out int first)) return;

            // Re-parent when the game moves the player between containers (layer changes,
            // room transitions). Same helper InitiateSprites uses.
            AttachEars(sLeaser, first, newContainer ?? rCam?.ReturnFContainer("Midground"));
        }

        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (!TryGetEarSprites(self, sLeaser, out EarData data, out int first)) return;

            for (int e = 0; e < EarCount; e++)
            {
                var mesh = sLeaser.sprites[first + e] as TriangleMesh;
                if (mesh == null) continue;

                Ear ear = data.ears[e];

                for (int i = 0; i < data.points; i++)
                {
                    Vector2 pos = Vector2.Lerp(ear.lastPos[i], ear.pos[i], timeStacker);

                    Vector2 dir = i == 0
                        ? DirVec(pos, Vector2.Lerp(ear.lastPos[1], ear.pos[1], timeStacker))
                        : DirVec(Vector2.Lerp(ear.lastPos[i - 1], ear.pos[i - 1], timeStacker), pos);

                    Vector2 perp = Perp(dir);

                    // Width profile along the ear. Linear taper gives a sharp triangle;
                    // an elliptical falloff holds the width longer and closes quickly at the
                    // very end, which reads as a rounded point. data.tip blends between them.
                    float t = i / (float)(data.points - 1);
                    float sharp = 1f - t;
                    float rounded = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                    float halfWidth = data.width * Mathf.Lerp(sharp, rounded, data.tip);

                    mesh.MoveVertice(i * 2, pos - perp * halfWidth - camPos);
                    mesh.MoveVertice(i * 2 + 1, pos + perp * halfWidth - camPos);
                }

                mesh.color = self.player.ShortCutColor();
            }
        }
    }
}
