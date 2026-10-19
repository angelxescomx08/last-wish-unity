using UnityEditor;
using UnityEngine;

namespace LastWish.Editor
{
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Game/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            // Pixel art is shown with hard edges (nearest); card faces, packs and the card back are
            // smoothscaled in the original, so they sample bilinearly (the Fx masks stay exact).
            var cards = assetPath.StartsWith("Assets/Resources/Game/Cards/");
            var exact = assetPath.StartsWith("Assets/Resources/Game/Cards/Placeholder/") || assetPath.StartsWith("Assets/Resources/Game/Cards/Fx/mask");
            importer.filterMode = cards && !exact ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Game/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            var music = assetPath.EndsWith("last_wish_theme.wav");
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
        }
    }
}
