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
        // ����Ƽ ���弼�� API�� ���
        // ���� Ÿ�� ���� => ������, ��, �ȵ���̵�, IOS �� ��ɷ� ������ ��
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows
        );

        // ������ Ŭ���̾�Ʈ(�÷��̾�) ���� ��ŭ �ݺ��� ����
        for (int i = 1; i <= playerCount; i++)
        {
            // ���� ��θ� �߰�
            // ������Ʈ �̸��� �÷��̾� ��ȣ�� ����ؼ� ���� ��ο� ���� ����
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