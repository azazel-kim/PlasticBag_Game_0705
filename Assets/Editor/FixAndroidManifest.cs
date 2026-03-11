// FixAndroidManifest.cs - 빌드 후처리 스크립트
// Android XR (Samsung Galaxy XR) 에서 앱이 런처에 보이도록 매니페스트를 수정합니다.
// Unity 6는 Android XR에서 UnityPlayerGameActivity를 메인으로 사용하지만
// LAUNCHER intent-filter가 없어서 앱 목록에 나타나지 않는 문제를 해결합니다.
using UnityEditor.Android;
using System.IO;
using System.Xml;

// IPostGenerateGradleAndroidProject: Gradle 프로젝트 생성 직후 호출되는 콜백
public class FixAndroidManifest : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 99;

    private const string AndroidNS = "http://schemas.android.com/apk/res/android";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // === 1. unityLibrary의 매니페스트에서 UnityPlayerActivity의 LAUNCHER 제거 ===
        // (구 Activity로 실행되면 크래시하므로)
        string mainManifestPath = path + "/src/main/AndroidManifest.xml";
        FixMainManifest(mainManifestPath);

        // === 2. xrmanifest의 매니페스트에서 GameActivity에 LAUNCHER 추가 ===
        // (이 Activity가 Android XR의 실제 메인 Activity)
        string xrManifestPath = path + "/xrmanifest.androidlib/AndroidManifest.xml";
        if (File.Exists(xrManifestPath))
        {
            FixXrManifest(xrManifestPath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[FixAndroidManifest] xrmanifest를 찾을 수 없습니다: " + xrManifestPath);
        }
    }

    // UnityPlayerActivity를 매니페스트에서 완전히 제거합니다
    // Android XR에서는 UnityPlayerGameActivity만 사용하며,
    // UnityPlayerActivity가 존재하면 Samsung XR 런처가 잘못된 Activity를 실행하여 크래시합니다.
    private void FixMainManifest(string manifestPath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(manifestPath);

        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("android", AndroidNS);

        // UnityPlayerActivity를 완전히 제거
        XmlNode activityNode = xmlDoc.SelectSingleNode(
            "//activity[@android:name='com.unity3d.player.UnityPlayerActivity']",
            nsManager);

        if (activityNode != null)
        {
            activityNode.ParentNode.RemoveChild(activityNode);
            UnityEngine.Debug.Log("[FixAndroidManifest] UnityPlayerActivity를 매니페스트에서 완전히 제거했습니다.");
        }

        // application 태그에 AppCompat 테마 설정 (GameActivity 호환성)
        XmlNode appNode = xmlDoc.SelectSingleNode("//application");
        if (appNode != null)
        {
            ((XmlElement)appNode).SetAttribute("theme", AndroidNS, "@style/Theme.AppCompat.NoActionBar");
            UnityEngine.Debug.Log("[FixAndroidManifest] Application 테마를 AppCompat으로 변경했습니다.");
        }

        xmlDoc.Save(manifestPath);
    }

    // UnityPlayerGameActivity에 LAUNCHER intent-filter를 추가합니다
    private void FixXrManifest(string manifestPath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(manifestPath);

        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("android", AndroidNS);

        // UnityPlayerGameActivity 찾기
        XmlNode gameActivityNode = xmlDoc.SelectSingleNode(
            "//activity[@android:name='com.unity3d.player.UnityPlayerGameActivity']",
            nsManager);

        if (gameActivityNode != null)
        {
            // exported="true" 설정
            ((XmlElement)gameActivityNode).SetAttribute("exported", AndroidNS, "true");

            // AppCompat 테마 설정 (GameActivity는 AppCompatActivity를 상속하므로 필수)
            ((XmlElement)gameActivityNode).SetAttribute("theme", AndroidNS, "@style/Theme.AppCompat.NoActionBar");

            // 기존 intent-filter 확인
            XmlNode existingFilter = gameActivityNode.SelectSingleNode("intent-filter");
            if (existingFilter == null)
            {
                // intent-filter 생성
                XmlElement intentFilter = xmlDoc.CreateElement("intent-filter");

                // <action android:name="android.intent.action.MAIN" />
                XmlElement action = xmlDoc.CreateElement("action");
                action.SetAttribute("name", AndroidNS, "android.intent.action.MAIN");
                intentFilter.AppendChild(action);

                // <category android:name="android.intent.category.LAUNCHER" />
                XmlElement launcherCat = xmlDoc.CreateElement("category");
                launcherCat.SetAttribute("name", AndroidNS, "android.intent.category.LAUNCHER");
                intentFilter.AppendChild(launcherCat);

                // <category android:name="android.intent.category.DEFAULT" />
                XmlElement defaultCat = xmlDoc.CreateElement("category");
                defaultCat.SetAttribute("name", AndroidNS, "android.intent.category.DEFAULT");
                intentFilter.AppendChild(defaultCat);

                gameActivityNode.AppendChild(intentFilter);
                UnityEngine.Debug.Log("[FixAndroidManifest] UnityPlayerGameActivity에 LAUNCHER intent-filter를 추가했습니다.");
            }
        }

        xmlDoc.Save(manifestPath);
    }
}
