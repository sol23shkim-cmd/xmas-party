// AcRenameProject.cs — 메뉴 "XmasParty/프로젝트 폴더 이름 바꾸기 (xmas-plaza → xmas-party)"
// Unity는 열려 있는 자기 폴더 이름을 못 바꾸므로: 작은 배치 파일을 만들어 실행하고 Unity를 닫음 →
// 배치 파일이 Unity가 꺼지길 기다렸다가 E:\dev\xmas-plaza\xmas-plaza → E:\dev\xmas-party\xmas-party 로 바꾸고 Unity를 새 경로로 다시 엶.
#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AcRenameProject
{
    [MenuItem("XmasParty/프로젝트를 xmas-party 폴더로 옮기기")]
    public static void Rename()
    {
        string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));      // ...\xmas-plaza\xmas-plaza
        string parent = Path.GetDirectoryName(proj);                                     // ...\xmas-plaza
        string root = Path.GetDirectoryName(parent);                                     // E:\dev
        if (Path.GetFileName(proj) != "xmas-plaza" || Path.GetFileName(parent) != "xmas-plaza")
        {
            UnityEngine.Debug.Log("[AcRename] 이미 바뀌었거나 예상과 다른 위치: " + proj);
            return;
        }
        string newParent = Path.Combine(root, "xmas-party");
        string newProj = Path.Combine(newParent, "xmas-party");
        string unity = EditorApplication.applicationPath.Replace('/', '\\');
        int pid = Process.GetCurrentProcess().Id;
        string bat = Path.Combine(Path.GetTempPath(), "xmas_rename.bat");
        string log = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "xmas_rename_log.txt");
        File.WriteAllText(bat,
            "@echo off\r\nsetlocal enabledelayedexpansion\r\n" +
            "chcp 65001 >nul\r\n" +
            ":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul && (timeout /t 2 /nobreak >nul & goto wait)\r\n" +
            "timeout /t 3 /nobreak >nul\r\n" +
            // Unity가 남긴 도우미 프로그램(셰이더 컴파일러, 빌드 서버 등) 정리
            "for %%x in (UnityShaderCompiler.exe Unity.ILPP.Runner.exe bee_backend.exe UnityCrashHandler64.exe) do taskkill /F /IM %%x >nul 2>&1\r\n" +
            $"echo start > \"{log}\"\r\n" +
            // 원래 폴더는 다른 프로그램(폴더 연결·검색 색인 등)이 잡고 있어 이름을 못 바꿈 → 새 이름으로 통째 복사해서 그쪽을 엶 (원래 폴더는 그대로 남음)
            $"robocopy \"{parent}\" \"{newParent}\" /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /XD \"{Path.Combine(proj, "Temp")}\" >> \"{log}\" 2>&1\r\n" +
            $"if exist \"{Path.Combine(newParent, "xmas-plaza")}\" ren \"{Path.Combine(newParent, "xmas-plaza")}\" \"xmas-party\" >> \"{log}\" 2>&1\r\n" +
            $"if exist \"{newProj}\\xmas-plaza.slnx\" del \"{newProj}\\xmas-plaza.slnx\"\r\n" +
            $"set P={proj}\r\n" +
            $"if exist \"{newProj}\" set P={newProj}\r\n" +
            $"echo open %P% >> \"{log}\"\r\n" +
            $"start \"\" \"{unity}\" -projectPath \"%P%\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"\" /min \"" + bat + "\"") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
        EditorApplication.Exit(0);
    }
}
#endif
