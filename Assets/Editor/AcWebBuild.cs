// AcWebBuild.cs — 메뉴 "XmasPlaza/웹 빌드 (휴대폰용)" 한 번으로 웹(WebGL) 빌드를 만들어요.
// 결과: <프로젝트>/Build/Web  (index.html + Build/ + StreamingAssets/)  → GitHub Pages 등에 그대로 올리면 휴대폰에서 링크로 열림
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AcWebBuild
{
    [MenuItem("XmasPlaza/웹 빌드 (휴대폰용)")]
    public static void BuildWeb()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        // 압축: gzip + 서버 설정이 없어도 풀리도록(decompression fallback) — GitHub Pages용
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "크리스마스 파티";
        PlayerSettings.companyName = "xmas-plaza";

        var scene = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scene)) scene = "Assets/Scenes/SampleScene.unity";
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Web"));
        Directory.CreateDirectory(outDir);

        var opt = new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };
        BuildReport rep = BuildPipeline.BuildPlayer(opt);
        Debug.Log($"[AcWebBuild] {rep.summary.result}  {rep.summary.totalSize / 1048576f:0.0}MB  {rep.summary.totalTime}  → {outDir}");

    }
}
#endif
