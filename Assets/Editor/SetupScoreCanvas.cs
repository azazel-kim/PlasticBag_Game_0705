using UnityEditor;
using UnityEngine;

public class SetupScoreCanvas
{
    [MenuItem("Tools/Setup Score Canvas (Lazy Follow)")]
    public static void Setup()
    {
        // ScoreCanvas 찾기
        GameObject scoreCanvas = GameObject.Find("ScoreCanvas");
        if (scoreCanvas == null)
        {
            Debug.LogError("[SetupScoreCanvas] ScoreCanvas not found!");
            return;
        }

        // 부모에서 분리 (루트로 이동)
        scoreCanvas.transform.SetParent(null);
        scoreCanvas.transform.position = new Vector3(0.2f, 1.5f, 0.6f);
        scoreCanvas.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);

        // ScoreFollowCamera 컴포넌트 추가 (중복 방지)
        if (scoreCanvas.GetComponent<ScoreFollowCamera>() == null)
        {
            scoreCanvas.AddComponent<ScoreFollowCamera>();
        }

        // 씬 더티 마킹
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupScoreCanvas] ScoreCanvas를 루트로 이동하고 ScoreFollowCamera 추가 완료!");
    }
}
