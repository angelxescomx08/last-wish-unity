using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastWish.VfxLab.Editor
{
    /// <summary>Pixel-art import settings for Assets/VfxLab/Art (point, no mipmaps, uncompressed, no NPOT scaling).</summary>
    public sealed class VfxLabTextureImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/VfxLab/Art/";

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var importer = (TextureImporter)assetImporter;
            var file = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            // Emissive maps and the light falloff are read as light (rgb), so their transparent texels must stay black.
            importer.alphaIsTransparency = !(file.EndsWith("_glow") || file == "vfx_light");
        }
    }
}
