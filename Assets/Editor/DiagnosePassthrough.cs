using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;
using Google.XR.Extensions;
using System.Linq;

public static class DiagnosePassthrough
{
    [MenuItem("Build/Diagnose Passthrough Settings")]
    public static void Diagnose()
    {
        Debug.Log("=== Passthrough Diagnosis Start ===");

        // 1. OpenXR Settings
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            Debug.LogError("[FAIL] OpenXR Settings for Android not found!");
            return;
        }

        // 2. Environment Blend Mode
        var blendFeature = settings.GetFeature<XREnvironmentBlendModeFeature>();
        if (blendFeature == null)
            Debug.LogError("[FAIL] XREnvironmentBlendModeFeature not found");
        else if (!blendFeature.enabled)
            Debug.LogError("[FAIL] XREnvironmentBlendModeFeature is DISABLED");
        else
            Debug.Log($"[OK] Environment Blend Mode: enabled, requestMode={blendFeature.RequestedEnvironmentBlendMode}");

        // 3. Check all features
        var features = settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>();
        foreach (var f in features)
        {
            string featureName = f.name ?? f.GetType().Name;
            if (f.enabled)
                Debug.Log($"[INFO] Feature ENABLED: {featureName}");
        }

        // 4. URP Asset
        var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            Debug.LogError("[FAIL] No URP Asset assigned in Graphics Settings!");
            return;
        }

        Debug.Log($"[INFO] URP Asset: {urpAsset.name}");
        Debug.Log($"[INFO] HDR: {urpAsset.supportsHDR}");
        Debug.Log($"[INFO] MSAA: {urpAsset.msaaSampleCount}");

        // 5. URP Renderer - TransparentBackgroundRendererFeature
        var rendererDataList = urpAsset.rendererDataList;
        bool hasTransparentBg = false;
        foreach (var rd in rendererDataList)
        {
            if (rd is UniversalRendererData urd)
            {
                foreach (var rf in urd.rendererFeatures)
                {
                    if (rf is TransparentBackgroundRendererFeature tbrf)
                    {
                        hasTransparentBg = true;
                        if (!tbrf.isActive)
                            Debug.LogError("[FAIL] TransparentBackgroundRendererFeature exists but INACTIVE!");
                        else
                            Debug.Log("[OK] TransparentBackgroundRendererFeature: active");
                    }
                }
            }
        }
        if (!hasTransparentBg)
            Debug.LogError("[FAIL] TransparentBackgroundRendererFeature NOT FOUND in any renderer!");

        // 6. Camera settings
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[WARN] No Main Camera in current scene");
        }
        else
        {
            Debug.Log($"[INFO] Camera clearFlags: {cam.clearFlags}");
            Debug.Log($"[INFO] Camera backgroundColor: {cam.backgroundColor} (alpha={cam.backgroundColor.a})");
            if (cam.backgroundColor.a > 0.01f)
                Debug.LogError("[FAIL] Camera background alpha is NOT transparent!");
            if (cam.clearFlags != CameraClearFlags.SolidColor)
                Debug.LogWarning("[WARN] Camera clearFlags should be SolidColor for passthrough");
        }

        // 7. XR Loader
        var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
        if (xrSettings == null)
            Debug.LogError("[FAIL] XRGeneralSettings.Instance is null");
        else
        {
            var manager = xrSettings.Manager;
            if (manager == null)
                Debug.LogError("[FAIL] XRManagerSettings is null");
            else
            {
                Debug.Log($"[INFO] AutomaticLoading: {manager.automaticLoading}");
                Debug.Log($"[INFO] AutomaticRunning: {manager.automaticRunning}");
                Debug.Log($"[INFO] Active Loaders: {string.Join(", ", manager.activeLoaders.Select(l => l.name))}");
            }
        }

        // 8. Player Settings
        var gfxAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        Debug.Log($"[INFO] Graphics APIs: {string.Join(", ", gfxAPIs)}");
        Debug.Log($"[INFO] Color Space: {PlayerSettings.colorSpace}");
        Debug.Log($"[INFO] Target SDK: {PlayerSettings.Android.targetSdkVersion}");

        Debug.Log("=== Passthrough Diagnosis Complete ===");
    }
}
