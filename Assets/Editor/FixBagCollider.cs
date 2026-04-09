using UnityEditor;
using UnityEngine;

public class FixBagCollider
{
    [MenuItem("Tools/Fix Bag Collider (MeshCollider → SphereCollider)")]
    public static void Fix()
    {
        string prefabPath = "Assets/garbage_bag_min.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("[FixBagCollider] 프리팹 없음"); return; }

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        // 기존 SphereCollider가 있으면 radius 10% 축소
        SphereCollider sc = instance.GetComponent<SphereCollider>();
        if (sc != null)
        {
            sc.radius = 0.0015f;
            Debug.Log($"[FixBagCollider] SphereCollider radius → 0.00177");
        }
        else
        {
            Debug.Log("[FixBagCollider] SphereCollider 없음");
        }

        PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        PrefabUtility.UnloadPrefabContents(instance);
        Debug.Log("[FixBagCollider] 프리팹 저장 완료!");
    }
}
