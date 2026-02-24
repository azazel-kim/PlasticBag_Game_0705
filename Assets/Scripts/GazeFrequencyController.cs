using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GazeFrequencyController : MonoBehaviour
{
    [Header("Core Settings")]
    public List<GameObject> targetObjects;
    public float updateInterval = 10f;
    public Canvas uiCanvas;
    public GameObject uiPanelPrefab;

    [Header("UI Panel Layout")]
    public Vector2 panelSize = new Vector2(320, 60); // 패널 하나의 크기
    public float panelSpacing = 10f; // 패널 사이의 간격
    public float startYPosition = 0f; // 패널이 시작될 Y 위치 (캔버스 기준)

    [Header("Canvas Position")]
    public float canvasDistance = 2f; // 카메라로부터 캔버스의 거리
    public float canvasVerticalOffset = 0f; // 캔버스의 상하 위치 조정 (양수: 위, 음수: 아래)

    private Dictionary<GameObject, float> gazeTimeDict = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, Text> objectNameTexts = new Dictionary<GameObject, Text>();
    private Dictionary<GameObject, Text> freqTexts = new Dictionary<GameObject, Text>();
    private float timer = 0f;
    private GameObject currentGazedObject = null;
    private float currentGazeStartTime = 0f;
    private List<GameObject> uiPanels = new List<GameObject>();

    void Start()
    {
        foreach (var obj in targetObjects)
        {
            gazeTimeDict[obj] = 0f;
        }

        // UI 생성 (수정됨: 인스펙터의 public 변수를 사용하도록 변경)
        for (int i = 0; i < targetObjects.Count; i++)
        {
            var panel = Instantiate(uiPanelPrefab, uiCanvas.transform);
            var rect = panel.GetComponent<RectTransform>();

            // 인스펙터에서 설정한 값으로 크기와 위치를 지정합니다.
            rect.sizeDelta = panelSize;
            rect.anchoredPosition = new Vector2(0, startYPosition - i * (panelSize.y + panelSpacing));

            var texts = panel.GetComponentsInChildren<Text>();
            foreach (var t in texts)
            {
                if (t.gameObject.name == "ObjectNameText") objectNameTexts[targetObjects[i]] = t;
                if (t.gameObject.name == "FreqText") freqTexts[targetObjects[i]] = t;
            }
            objectNameTexts[targetObjects[i]].text = targetObjects[i].name;
            freqTexts[targetObjects[i]].text = "0%";
            uiPanels.Add(panel);
        }

        // Canvas를 카메라 앞에 배치 (수정됨: 인스펙터의 public 변수 사용)
        if (uiCanvas.renderMode == RenderMode.WorldSpace)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                // 인스펙터에서 설정한 거리와 높이로 캔버스 위치를 조정합니다.
                uiCanvas.transform.position = cam.transform.position + (cam.transform.forward * canvasDistance) + (cam.transform.up * canvasVerticalOffset);
                uiCanvas.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                
                // 월드 스페이스 캔버스의 크기는 적절히 조절해야 합니다.
                // uiCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 400); // 필요시 조절
                // uiCanvas.transform.localScale = Vector3.one * 0.002f; // 필요시 조절
            }
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        Ray gazeRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;
        GameObject hitObj = null;
        if (Physics.Raycast(gazeRay, out hit, 10f))
        {
            if (targetObjects.Contains(hit.collider.gameObject))
            {
                hitObj = hit.collider.gameObject;
            }
        }
        
        if (hitObj != currentGazedObject)
        {
            if (currentGazedObject != null)
            {
                gazeTimeDict[currentGazedObject] += Time.time - currentGazeStartTime;
            }
            currentGazedObject = hitObj;
            currentGazeStartTime = Time.time;
        }
        
        if (timer >= updateInterval)
        {
            float total = 0f;
            foreach (var obj in targetObjects)
                total += gazeTimeDict[obj];
            foreach (var obj in targetObjects)
            {
                float percent = (total > 0) ? (gazeTimeDict[obj] / total) * 100f : 0f;
                freqTexts[obj].text = percent.ToString("F1") + "%";
            }
            
            foreach (var obj in targetObjects)
                gazeTimeDict[obj] = 0f;
            timer = 0f;
        }
    }
}