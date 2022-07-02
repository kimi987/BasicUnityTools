using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EditorTool : Editor
{
    [MenuItem("Tools/Editor Tools/打开游戏场景 %g")]
    static void OpenGameScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Start.unity");
    }


    /// <summary>
    /// 清空控制台内容
    /// </summary>
    public static void ClearConsole()
    {
        Type log = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
        var clearMethod = log.GetMethod("Clear");
        clearMethod.Invoke(null, null);
    }

    [MenuItem("Tools/Editor Tools/重导AC")]
    public static void ReimportAnimationController()
    {
        var acs = Selection.GetFiltered<AnimatorController>(SelectionMode.DeepAssets);

        foreach (var ac in acs)
        {
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(ac));
        }
        
    }
}
