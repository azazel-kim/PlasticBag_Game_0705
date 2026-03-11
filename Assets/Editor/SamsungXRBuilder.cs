using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// Samsung XR (Android XR) 빌드 자동화 스크립트.
/// Build 메뉴에서 Development/Release 빌드를 원클릭으로 실행합니다.
/// </summary>
public static class SamsungXRBuilder
{
    private const string BuildFolder = "Builds";
    private const string AppName = "PlasticBagGame_SamsungXR";

    [MenuItem("Build/Samsung XR - Development APK")]
    public static void BuildDevelopment()
    {
        Build(isDevelopment: true);
    }

    [MenuItem("Build/Samsung XR - Release APK")]
    public static void BuildRelease()
    {
        Build(isDevelopment: false);
    }

    private static void Build(bool isDevelopment)
    {
        string suffix = isDevelopment ? "Dev" : "Release";
        string apkPath = Path.Combine(BuildFolder, $"{AppName}_{suffix}.apk");

        Directory.CreateDirectory(BuildFolder);

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[SamsungXRBuilder] EditorBuildSettings에 활성화된 씬이 없습니다.");
            return;
        }

        ValidateSettings();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = isDevelopment
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        Debug.Log($"[SamsungXRBuilder] {suffix} 빌드 시작... 출력: {apkPath}");

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            float sizeMB = summary.totalSize / (1024f * 1024f);
            Debug.Log($"[SamsungXRBuilder] 빌드 성공! 크기: {sizeMB:F1}MB, 시간: {summary.totalTime}");
        }
        else
        {
            Debug.LogError($"[SamsungXRBuilder] 빌드 실패: {summary.result}, 오류 {summary.totalErrors}개");
        }
    }

    private static void ValidateSettings()
    {
        // IL2CPP 확인
        var androidTarget = UnityEditor.Build.NamedBuildTarget.Android;
        if (PlayerSettings.GetScriptingBackend(androidTarget) != ScriptingImplementation.IL2CPP)
        {
            Debug.LogWarning("[SamsungXRBuilder] Scripting Backend이 IL2CPP가 아닙니다. IL2CPP로 변경합니다.");
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
        }

        // ARM64 확인
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
        {
            Debug.LogWarning("[SamsungXRBuilder] Target Architecture가 ARM64가 아닙니다. ARM64로 변경합니다.");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        // Target SDK 확인
        if ((int)PlayerSettings.Android.targetSdkVersion < 35)
        {
            Debug.LogWarning($"[SamsungXRBuilder] Target SDK가 {PlayerSettings.Android.targetSdkVersion}입니다. API 35를 권장합니다.");
        }

        // Version Code 자동 증가
        int currentCode = PlayerSettings.Android.bundleVersionCode;
        PlayerSettings.Android.bundleVersionCode = currentCode + 1;
        Debug.Log($"[SamsungXRBuilder] Bundle Version Code: {currentCode} → {currentCode + 1}");
    }

    [MenuItem("Build/Samsung XR - Validate Settings Only")]
    public static void ValidateOnly()
    {
        Debug.Log("[SamsungXRBuilder] 설정 검증 시작...");
        ValidateSettings();

        // Graphics API 확인
        var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        Debug.Log($"[SamsungXRBuilder] Graphics APIs: {string.Join(", ", graphicsAPIs)}");

        // 씬 목록 확인
        var scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
        {
            string status = scene.enabled ? "활성" : "비활성";
            Debug.Log($"[SamsungXRBuilder] 씬 [{status}]: {scene.path}");
        }

        Debug.Log("[SamsungXRBuilder] 설정 검증 완료.");
    }
}
