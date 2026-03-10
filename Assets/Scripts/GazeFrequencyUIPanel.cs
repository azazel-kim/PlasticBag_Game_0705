using UnityEngine;
using UnityEngine.UI;

public class GazeFrequencyUIPanel : MonoBehaviour
{
    public Text objectNameText;
    public Text freqText;
    void Awake()
    {
        // 배경 반투명 설정
        var img = GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = 0.5f;
            img.color = c;
        }
    }
}