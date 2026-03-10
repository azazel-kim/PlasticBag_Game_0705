using UnityEngine;
using UnityEngine.UI;

public class GazeFrequencyUIFactory : MonoBehaviour
{
    public static GameObject CreatePanel(Transform parent, string objectName, float percent)
    {
        GameObject panel = new GameObject("GazeUIPanel");
        panel.transform.SetParent(parent);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0,0,0,0.5f); // 반투명 검정
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320, 60);
        // 오브젝트 이름
        GameObject nameObj = new GameObject("ObjectNameText");
        nameObj.transform.SetParent(panel.transform);
        var nameText = nameObj.AddComponent<Text>();
        nameText.text = objectName;
        nameText.fontSize = 24;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        var nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0,0);
        nameRect.anchorMax = new Vector2(0.5f,1);
        nameRect.offsetMin = new Vector2(10,0);
        nameRect.offsetMax = new Vector2(-10,0);
        // 빈도수
        GameObject freqObj = new GameObject("FreqText");
        freqObj.transform.SetParent(panel.transform);
        var freqText = freqObj.AddComponent<Text>();
        freqText.text = percent.ToString("F1") + "%";
        freqText.fontSize = 24;
        freqText.alignment = TextAnchor.MiddleRight;
        freqText.color = Color.white;
        var freqRect = freqObj.GetComponent<RectTransform>();
        freqRect.anchorMin = new Vector2(0.5f,0);
        freqRect.anchorMax = new Vector2(1,1);
        freqRect.offsetMin = new Vector2(10,0);
        freqRect.offsetMax = new Vector2(-10,0);
        return panel;
    }
}