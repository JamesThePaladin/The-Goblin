using System;
using System.IO;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// Puts a custom texture on the Goblin's tail.
    ///
    /// The tail is sprite index 2: a TriangleMesh with a UV-mapped texture, NOT a frame in the
    /// 87-sprite sheet. That makes it the one piece of custom art that needs no atlas packing,
    /// no frame names and no anchor rules — see PLAN.md Feature 3.
    ///
    /// Vanilla builds the mesh with the "Futile_White" element and never sets UVvertices, since
    /// a flat white tail doesn't need them. So we supply both: the element, and a UV mapping.
    /// </summary>
    internal static class GoblinTail
    {
        private const string AtlasName = "goblinTail";
        private const string TexturePath = "illustrations/dottedTail.png";
        private const int TailSpriteIndex = 2;

        private static FAtlasElement _element;

        public static void Apply()
        {
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
        }

        /// <summary>Called from OnModsInit, once the game's file system is ready.</summary>
        public static void LoadTexture(BepInEx.Logging.ManualLogSource log)
        {
            try
            {
                string path = AssetManager.ResolveFilePath(TexturePath);
                if (!File.Exists(path))
                {
                    log?.LogError($"Tail texture not found at '{TexturePath}' (resolved: {path}).");
                    return;
                }

                // Point filtering: this is pixel art, bilinear would blur it.
                var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) { filterMode = FilterMode.Point };
                texture.LoadImage(File.ReadAllBytes(path));

                Futile.atlasManager.LoadAtlasFromTexture(AtlasName, texture, false);
                _element = Futile.atlasManager.GetElementWithName(AtlasName);

                log?.LogInfo($"Loaded tail texture {texture.width}x{texture.height} as '{AtlasName}'.");
            }
            catch (Exception e)
            {
                log?.LogError($"Failed to load tail texture: {e}");
            }
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);

            if (_element == null) return;
            if (self?.player == null || self.player.SlugCatClass?.value != Plugin.GoblinName) return;
            if (sLeaser?.sprites == null || sLeaser.sprites.Length <= TailSpriteIndex) return;

            if (!(sLeaser.sprites[TailSpriteIndex] is TriangleMesh mesh)) return;

            mesh.element = _element;
            MapUVs(mesh);
        }

        /// <summary>
        /// Maps the texture along the tail.
        ///
        /// The vanilla tail mesh has (segments * 4 - 1) vertices — 15 for the standard 4
        /// segments — laid out as consecutive left/right pairs running base to tip, finishing
        /// with a single vertex at the very tip. Read off the triangle list, which walks
        /// (0,1,2) (1,2,3) then bridges (2,3,4) (3,4,5) and so on, ending at vertex 14.
        ///
        /// So: u runs 0 (base) to 1 (tip) along the pairs, v spans 0..1 across the width. That
        /// matches how the sprite guide describes tail textures — the mid-band reads as the
        /// underbelly, and detail stretches and compresses toward the tip.
        /// </summary>
        private static void MapUVs(TriangleMesh mesh)
        {
            if (mesh.UVvertices == null || mesh.UVvertices.Length == 0) return;

            int count = mesh.UVvertices.Length;
            int pairs = (count - 1) / 2;
            if (pairs < 1) return;

            for (int i = 0; i < pairs; i++)
            {
                float u = i / (float)pairs;
                mesh.UVvertices[i * 2] = new Vector2(u, 0f);
                mesh.UVvertices[i * 2 + 1] = new Vector2(u, 1f);
            }

            // Odd vertex out: the tip, centred across the texture's width.
            mesh.UVvertices[count - 1] = new Vector2(1f, 0.5f);
        }
    }
}
