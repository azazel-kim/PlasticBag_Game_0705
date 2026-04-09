using UnityEditor;
using UnityEngine;

public class SetupBGM
{
    [MenuItem("Tools/Setup BGM Manager")]
    public static void Setup()
    {
        // 기존 BGMManager가 있으면 제거
        BGMManager existing = Object.FindFirstObjectByType<BGMManager>();
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        // 새 GameObject 생성
        GameObject bgmObj = new GameObject("BGMManager");

        // BGMManager 컴포넌트 추가
        BGMManager mgr = bgmObj.AddComponent<BGMManager>();

        // BGM 클립 로드
        string[] paths = {
            "Assets/Audio/BGM/Feeling_Happy.mp3",
            "Assets/Audio/BGM/Smile.mp3",
            "Assets/Audio/BGM/Comical.mp3",
            "Assets/Audio/BGM/Keep_Smiling.mp3"
        };

        AudioClip[] clips = new AudioClip[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
            if (clips[i] == null)
                Debug.LogWarning($"[SetupBGM] 클립 없음: {paths[i]}");
            else
                Debug.Log($"[SetupBGM] 로드: {clips[i].name} ({clips[i].length:F1}s)");
        }

        mgr.bgmClips = clips;
        mgr.volume = 0.25f;

        // 씬 더티
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupBGM] BGMManager 생성 완료! 4곡 랜덤 셔플 재생");
    }
}
