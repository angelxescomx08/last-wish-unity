using UnityEditor;
using UnityEngine;

namespace LastWish.Editor
{
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Game/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = assetPath.EndsWith("CardFrame.png") ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Game/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            var music = assetPath.EndsWith("Music.wav");
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
        }
    }
}
