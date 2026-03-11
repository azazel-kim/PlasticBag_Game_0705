using UnityEditor;

public class EnableBuildScenes
{
    [MenuItem("Build/Enable All Build Scenes")]
    public static void EnableAll()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
        {
            scene.enabled = true;
        }
        EditorBuildSettings.scenes = scenes;
        UnityEngine.Debug.Log("[Build] 모든 씬 활성화 완료: " + scenes.Length + "개");
    }
}
