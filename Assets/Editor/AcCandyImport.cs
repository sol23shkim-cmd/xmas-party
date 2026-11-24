// AcCandyImport.cs - 캔디 팡 그림(Resources/Candy) 가져오기 설정: 압축 없이 깨끗하게, 투명 테두리 번짐 없이
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class AcCandyImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Candy/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = assetPath.EndsWith("bg.png") || assetPath.EndsWith("title.png") ? 1024 : 256;
    }
}
#endif
