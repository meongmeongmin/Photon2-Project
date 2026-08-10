using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MultiplayerBuildAndRun
{
    [MenuItem("Tools/Run Multiplayer/Win64/1 Players")]
    static void PerformWin64Build1()
    {
        PerformWin64Build(1);
    }

    #region Window
    [MenuItem("Tools/Run Multiplayer/Win64/2 Players")]
    static void PerformWin64Build2()
    {
        PerformWin64Build(2);
    }

    [MenuItem("Tools/Run Multiplayer/Win64/3 Players")]
    static void PerformWin64Build3()
    {
        PerformWin64Build(3);
    }

    [MenuItem("Tools/Run Multiplayer/Win64/4 Players")]
    static void PerformWin64Build4()
    {
        PerformWin64Build(4);
    }

    static void PerformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);

        string exePath = "Builds/Win64/" + GetProjectName() + "/" + GetProjectName() + ".exe";
        string fullExePath = Path.GetFullPath(exePath);

        BuildPipeline.BuildPlayer(GetScenePaths(), exePath,
            BuildTarget.StandaloneWindows64, BuildOptions.None);

        for (int i = 0; i < playerCount; i++)
        {
            Process.Start(fullExePath);
        }
    }
    #endregion

    #region Mac
    [MenuItem("Tools/Run Multiplayer/Mac/1 Players")]
    static void PerformMacBuild1()
    {
        PerformMacBuild(1);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/2 Players")]
    static void PerformMacBuild2()
    {
        PerformMacBuild(2);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/3 Players")]
    static void PerformMacBuild3()
    {
        PerformMacBuild(3);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/4 Players")]
    static void PerformMacBuild4()
    {
        PerformMacBuild(4);
    }

    static void PerformMacBuild(int playerCount)
    {
        // 유니티 에디터 API로 현재 활성 빌드 플랫폼을 바꾼다.
        // 빌드 플랫폼은 PC(Windows/Mac), 콘솔, 모바일 등 실행 기기 종류를 뜻한다.
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows
        );

        // 같은 빌드를 접속 인원 수만큼 복사해서 만든다 (로컬에서 여러 창을 띄워 멀티플레이를 테스트하기 위함).
        for (int i = 1; i <= playerCount; i++)
        {
            // 저장 경로에 프로젝트 이름과 순서 번호를 붙인다
            // (1, 2, 3...번호를 붙여서 결과물끼리 이름이 겹치지 않게 한다)
            BuildPipeline.BuildPlayer(GetScenePaths(),
                "Builds/Win64/" + GetProjectName() + i.ToString() + "/" + GetProjectName() + i.ToString() + ".app",
                BuildTarget.StandaloneOSX, BuildOptions.AutoRunPlayer
            );
        }
    }
    #endregion

    static string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        return s[s.Length - 2];
    }

    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];

        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        return scenes;
    }
}