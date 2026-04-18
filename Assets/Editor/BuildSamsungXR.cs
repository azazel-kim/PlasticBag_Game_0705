using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

public class BuildSamsungXR
{
    [MenuItem("Build/Build Samsung XR APK")]
    public static void BuildAPK()
    {
        string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
        string buildDir = Path.Combine(projectPath, "Builds");
        Directory.CreateDirectory(buildDir);

        // productName에서 숫자 접두사를 +1 증가
        // 예: "1-HandPhysicsRTest" → "2-HandPhysicsRTest"
        string productName = PlayerSettings.productName;
        var match = Regex.Match(productName, @"^(\d+)(-.+)$");
        if (match.Success)
        {
            int num = int.Parse(match.Groups[1].Value) + 1;
            string newName = $"{num}{match.Groups[2].Value}";
            PlayerSettings.productName = newName;
            productName = newName;
            Debug.Log($"[Build] 앱 이름 자동 증가: {match.Value} → {newName}");
        }

        string apkPath = Path.Combine(projectPath, $"{productName}.apk");

        // 빌드 설정
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = Array.ConvertAll(
            Array.FindAll(EditorBuildSettings.scenes, s => s.enabled),
            scene => scene.path
        );
        buildPlayerOptions.locationPathName = apkPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;

        // 빌드 번호 증가
        int currentBuildNumber = PlayerSettings.Android.bundleVersionCode;
        PlayerSettings.Android.bundleVersionCode = currentBuildNumber + 1;

        Debug.Log($"=== Samsung XR APK 빌드 시작 ===");
        Debug.Log($"출력 경로: {apkPath}");
        Debug.Log($"빌드 번호: {currentBuildNumber} → {PlayerSettings.Android.bundleVersionCode}");
        Debug.Log($"씬 개수: {buildPlayerOptions.scenes.Length}");

        // 빌드 실행
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"\n✓ 빌드 성공!");
            Debug.Log($"APK 경로: {apkPath}");
            Debug.Log($"빌드 크기: {(summary.totalSize / 1024f / 1024f):F2} MB");
            Debug.Log($"빌드 시간: {summary.totalTime.TotalSeconds:F1}초");

            // 빌드 결과 파일 확인
            if (File.Exists(apkPath))
            {
                FileInfo fileInfo = new FileInfo(apkPath);
                Debug.Log($"파일 크기: {(fileInfo.Length / 1024f / 1024f):F2} MB");
                Debug.Log($"APK 파일명: {Path.GetFileName(apkPath)}");
            }
        }
        else
        {
            Debug.LogError($"✗ 빌드 실패!");
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error)
                    {
                        Debug.LogError($"[{step.name}] {message.content}");
                    }
                }
            }
        }
    }
}
