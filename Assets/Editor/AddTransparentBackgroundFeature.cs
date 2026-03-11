using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Google.XR.Extensions;
using System.Linq;

/// <summary>
/// URP Renderer에 패스쓰루 배경 Feature들을 추가합니다.
/// - Google TransparentBackgroundRendererFeature: OpenXR 빌드 검증 통과용
/// - PassthroughBackgroundRendererFeature: 실제 Legacy 렌더 경로 실행용
/// </summary>
public static class AddTransparentBackgroundFeature
{
    [MenuItem("Build/Setup Passthrough Background Feature")]
    public static void SetupFeature()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");

        if (guids.Length == 0)
        {
            Debug.LogError("[PassthroughSetup] UniversalRendererData 에셋을 찾을 수 없습니다.");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rendererData == null) continue;

            bool changed = false;

            // 1. Google TransparentBackgroundRendererFeature (빌드 검증용)
            bool hasGoogle = rendererData.rendererFeatures
                .Any(f => f is TransparentBackgroundRendererFeature);

            if (!hasGoogle)
            {
                var gFeature = ScriptableObject.CreateInstance<TransparentBackgroundRendererFeature>();
                gFeature.name = "TransparentBackgroundRendererFeature";
                gFeature.SetActive(true);
                AssetDatabase.AddObjectToAsset(gFeature, rendererData);
                rendererData.rendererFeatures.Add(gFeature);
                Debug.Log($"[PassthroughSetup] {path}: Google TransparentBackgroundRendererFeature 추가 (검증용)");
                changed = true;
            }

            // 2. 커스텀 PassthroughBackgroundRendererFeature (실제 실행용)
            bool hasCustom = rendererData.rendererFeatures
                .Any(f => f is PassthroughBackgroundRendererFeature);

            if (!hasCustom)
            {
                var cFeature = ScriptableObject.CreateInstance<PassthroughBackgroundRendererFeature>();
                cFeature.name = "PassthroughBackgroundRendererFeature";
                cFeature.SetActive(true);
                AssetDatabase.AddObjectToAsset(cFeature, rendererData);
                rendererData.rendererFeatures.Add(cFeature);
                Debug.Log($"[PassthroughSetup] {path}: PassthroughBackgroundRendererFeature 추가 (실행용)");
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(rendererData);
            else
                Debug.Log($"[PassthroughSetup] {path}: 모든 Feature 이미 존재");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PassthroughSetup] 완료!");
    }
}
