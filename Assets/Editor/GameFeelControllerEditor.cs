using UnityEditor;
using UnityEngine;

/// <summary>
/// GameFeelController의 Inspector를 확장하여
/// 현재 활성 프리셋의 파라미터를 0~1 정규화 슬라이더로 조절합니다.
/// 0 = 최소/없음, 1 = 최대 효과.
/// 내부적으로 실제 물리 값으로 자동 변환됩니다.
/// </summary>
[CustomEditor(typeof(GameFeelController))]
public class GameFeelControllerEditor : Editor
{
    private bool _presetFoldout = true;

    // 각 파라미터의 실제 값 범위 (min, max)
    // 0~1 슬라이더 ↔ 실제 값 변환에 사용
    static readonly (float min, float max) R_bounce    = (1f, 10f);
    static readonly (float min, float max) R_impMin    = (0.001f, 0.1f);
    static readonly (float min, float max) R_impMax    = (0.05f, 2f);
    // upwardBias, gravityOffset은 이미 0~1
    static readonly (float min, float max) R_gravityY  = (0f, -9.81f);  // 0=무중력, 1=지구
    static readonly (float min, float max) R_drag      = (0f, 5f);
    static readonly (float min, float max) R_mass      = (0.01f, 1f);
    static readonly (float min, float max) R_medium    = (0.1f, 5f);
    static readonly (float min, float max) R_strong    = (1f, 20f);
    static readonly (float min, float max) R_volume    = (1f, 10f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (GameFeelController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GameFeelPreset preset = controller.GetActivePreset();

        if (preset == null)
        {
            EditorGUILayout.HelpBox("활성 프리셋이 없습니다. 프리셋을 등록하세요.", MessageType.Warning);
            return;
        }

        _presetFoldout = EditorGUILayout.Foldout(_presetFoldout,
            $"현재 프리셋 조절: [{preset.displayName}]  (0~1 정규화)", true,
            EditorStyles.foldoutHeader);

        if (!_presetFoldout) return;

        SerializedObject presetSO = new SerializedObject(preset);
        presetSO.Update();

        EditorGUI.indentLevel++;

        // === 튕김 튜닝 ===
        EditorGUILayout.LabelField("튕김 튜닝", EditorStyles.boldLabel);
        DrawNormalized(presetSO, "bounceRestitution", "Bounce (튕김 세기: 0=약, 1=강)", R_bounce);
        DrawNormalized(presetSO, "impulseMin",        "Impulse Min (최소 힘: 0=없음, 1=강)", R_impMin);
        DrawNormalized(presetSO, "impulseMax",        "Impulse Max (최대 힘: 0=약, 1=강)", R_impMax);
        DrawDirect01(presetSO,   "upwardBias",        "Up Bias (위로 튀는 정도: 0=옆, 1=위)");
        DrawDirect01(presetSO,   "gravityOffset",     "Gravity Offset (부양감: 0=없음, 1=둥실)");

        EditorGUILayout.Space(5);

        // === 낙하 튜닝 ===
        EditorGUILayout.LabelField("낙하 튜닝", EditorStyles.boldLabel);
        DrawNormalized(presetSO, "gravityY",    "Gravity (중력: 0=무중력, 1=지구)", R_gravityY);
        DrawNormalized(presetSO, "linearDrag",  "Drag (공기 저항: 0=없음, 1=최대)", R_drag);
        DrawNormalized(presetSO, "mass",        "Mass (무게: 0=깃털, 1=무거움)", R_mass);

        EditorGUILayout.Space(5);

        // === 사운드 튜닝 ===
        EditorGUILayout.LabelField("사운드 튜닝", EditorStyles.boldLabel);
        DrawNormalized(presetSO, "mediumImpactThreshold", "Medium (중간 소리: 0=민감, 1=둔감)", R_medium);
        DrawNormalized(presetSO, "strongImpactThreshold", "Strong (강한 소리: 0=민감, 1=둔감)", R_strong);
        DrawNormalized(presetSO, "volumeMultiplier",      "Volume (볼륨: 0=작음, 1=큼)", R_volume);

        EditorGUI.indentLevel--;

        // 실제 값 표시 (접이식)
        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            $"실제 값: Bounce={preset.bounceRestitution:F2}, ImpMax={preset.impulseMax:F3}, " +
            $"Gravity={preset.gravityY:F2}, Mass={preset.mass:F3}", MessageType.None);

        if (presetSO.ApplyModifiedProperties())
        {
            if (Application.isPlaying)
                controller.ApplyPreset(controller.currentPresetIndex);
            EditorUtility.SetDirty(preset);
        }

        EditorGUILayout.Space(5);
        if (Application.isPlaying)
        {
            if (GUILayout.Button("프리셋 즉시 적용"))
                controller.ApplyPreset(controller.currentPresetIndex);
        }
    }

    /// <summary>
    /// 0~1 슬라이더 → 실제 값(min~max)으로 변환하여 저장
    /// </summary>
    private void DrawNormalized(SerializedObject so, string propName, string label, (float min, float max) range)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null) return;

        // 실제 값 → 0~1 정규화
        float actual = prop.floatValue;
        float normalized = Mathf.InverseLerp(range.min, range.max, actual);

        // 0~1 슬라이더 표시
        float newNormalized = EditorGUILayout.Slider(new GUIContent(label), normalized, 0f, 1f);

        // 변경되었으면 실제 값으로 역변환하여 저장
        if (!Mathf.Approximately(newNormalized, normalized))
        {
            prop.floatValue = Mathf.Lerp(range.min, range.max, newNormalized);
        }
    }

    /// <summary>
    /// 이미 0~1 범위인 파라미터는 그대로 표시
    /// </summary>
    private void DrawDirect01(SerializedObject so, string propName, string label)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null) return;

        EditorGUILayout.Slider(prop, 0f, 1f, new GUIContent(label));
    }
}
