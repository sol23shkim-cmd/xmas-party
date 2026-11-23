// AcAudioImport.cs - 음악·효과음 가져오기 설정 (웹 빌드 크기를 줄이고 바로 재생되게)
using UnityEditor;
using UnityEngine;

public class AcAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("Resources/Audio/")) return;
        var imp = (AudioImporter)assetImporter;
        bool sfx = assetPath.Contains("/Sfx/");
        var s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = sfx ? 0.6f : 0.3f;
        s.loadType = sfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        imp.defaultSampleSettings = s;
        imp.forceToMono = sfx;
        imp.loadInBackground = !sfx;
    }
}
