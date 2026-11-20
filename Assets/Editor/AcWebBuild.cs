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
        if (rep.summary.result == BuildResult.Succeeded)
        {
            // index.html 다듬기: 제목, 휴대폰·PC 모두 창 전체 화면
            string idx = Path.Combine(outDir, "index.html");
            string html = File.ReadAllText(idx);
            html = html.Replace("<title>Unity Web Player | ", "<title>")
                       .Replace("if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {", "if (true) {   // 휴대폰·PC 모두 브라우저 창 전체를 게임 화면으로");
            File.WriteAllText(idx, html);
        }
        Debug.Log($"[AcWebBuild] {rep.summary.result}  {rep.summary.totalSize / 1048576f:0.0}MB  {rep.summary.totalTime}  → {outDir}");

    }
}
#endif
