// C# 스크립트 이름: EyeGazeRaycaster.cs
// Unity의 기본 기능을 사용하기 위해 선언합니다.
using UnityEngine;

// 이 스크립트가 붙는 게임 오브젝트에는 반드시 LineRenderer 컴포넌트가 있도록 강제합니다.
[RequireComponent(typeof(LineRenderer))]
public class EyeGazeRaycaster : MonoBehaviour
{
    // ===================================================================
    // Inspector 창에서 직접 연결해야 하는 항목들
    // ===================================================================

    [Header("필수 연결 항목")] // Inspector 창에서 구분을 위한 헤더입니다.
    [Tooltip("Hierarchy 창의 OVRCameraRig > TrackingSpace > CenterEyeAnchor 오브젝트를 여기에 끌어다 놓으세요.")]
    public Transform eyeGazeAnchor; // 아이 트래킹의 기준점이 될 트랜스폼입니다. (CenterEyeAnchor)

    [Tooltip("포인터의 부모 오브젝트(DysonGazePointer)를 여기에 끌어다 놓으세요.")]
    public GameObject gazePointer; // 포인터 전체의 위치를 제어할 부모 게임 오브젝트입니다.

    [Tooltip("머티리얼을 변경할 자식 오브젝트(Sphere)를 여기에 끌어다 놓으세요.")]
    public Renderer targetRenderer; // 머티리얼을 바꿀 대상(Sphere)의 Renderer 컴포넌트입니다.

    [Header("머티리얼 설정")]
    [Tooltip("레이(광선)가 오브젝트에 닿았을 때 사용할 머티리얼입니다.")]
    public Material onHitMaterial; // 광선이 물체에 닿았을 때 포인터에 적용할 머티리얼입니다.

    [Tooltip("레이(광선)가 허공에 있을 때 사용할 머티리얼입니다.")]
    public Material offHitMaterial; // 광선이 허공에 있을 때 포인터에 적용할 머티리얼입니다.

    [Header("기타 설정")]
    [Tooltip("시선 추적 광선이 도달할 최대 거리입니다.")]
    public float maxGazeDistance = 10.0f; // 광선이 날아갈 최대 거리를 설정합니다.

    // ===================================================================
    // 스크립트 내부에서 사용할 비공개 변수
    // ===================================================================

    private LineRenderer gazeLineRenderer; // 시선 레이저를 그릴 Line Renderer 컴포넌트입니다.

    // 게임이 시작될 때 한번만 호출되는 함수입니다.
    void Start()
    {
        // 이 스크립트가 붙어있는 게임 오브젝트의 LineRenderer 컴포넌트를 가져와 변수에 저장합니다.
        gazeLineRenderer = GetComponent<LineRenderer>();

        // Inspector 창에서 모든 필수 항목들이 제대로 연결되었는지 확인합니다.
        if (eyeGazeAnchor == null || gazePointer == null || targetRenderer == null || onHitMaterial == null || offHitMaterial == null)
        {
            // 하나라도 연결이 안 되어 있으면, 콘솔에 에러 메시지를 출력하고 스크립트 작동을 멈춥니다.
            Debug.LogError("EyeGazeRaycaster의 Inspector 창에서 모든 항목을 연결해주세요!");
            this.enabled = false; // 스크립트를 비활성화시켜 Update 함수가 실행되지 않도록 합니다.
            return; // 즉시 Start 함수를 종료합니다.
        }
    }

    // 매 프레임마다 호출되는 함수입니다. 게임의 핵심 로직이 담깁니다.
    void Update()
    {
        // 아이 트래킹 기준점(CenterEyeAnchor)의 현재 위치와 바라보는 방향으로 광선(Ray)을 생성합니다.
        Ray gazeRay = new Ray(eyeGazeAnchor.position, eyeGazeAnchor.forward);

        // Line Renderer(레이저 선)의 시작점을 광선의 시작점(눈 위치)으로 설정합니다.
        gazeLineRenderer.SetPosition(0, gazeRay.origin);

        // Physics.Raycast를 사용해 광선을 쏩니다.
        if (Physics.Raycast(gazeRay, out RaycastHit hit, maxGazeDistance))
        {
            // --- 광선이 어떤 물체와 부딪혔을 경우 ---

            // 포인터 전체를 화면에 보이게 합니다.
            gazePointer.SetActive(true);
            // 포인터 부모 오브젝트의 위치를 광선이 부딪힌 정확한 지점으로 이동시킵니다.
            gazePointer.transform.position = hit.point;
            // 레이저 선의 끝점을 광선이 부딪힌 지점으로 설정합니다.
            gazeLineRenderer.SetPosition(1, hit.point);

            // 지정된 targetRenderer(Sphere)의 머티리얼을 '닿았을 때' 용으로 변경합니다.
            targetRenderer.material = onHitMaterial;

            // 디버깅을 위해 현재 바라보고 있는 오브젝트의 이름을 콘솔에 출력합니다.
            Debug.Log("바라보는 중: " + hit.collider.name);
        }
        else
        {
            // --- 광선이 아무것에도 부딪히지 않았을 경우 (허공을 볼 때) ---

            // 포인터 전체를 화면에 보이게 합니다.
            gazePointer.SetActive(true);
            // 포인터 부모 오브젝트의 위치를 광선 방향으로 최대 거리만큼 떨어진 곳에 위치시킵니다.
            gazePointer.transform.position = gazeRay.origin + gazeRay.direction * maxGazeDistance;
            // 레이저 선의 끝점도 최대 거리로 설정합니다.
            gazeLineRenderer.SetPosition(1, gazeRay.origin + gazeRay.direction * maxGazeDistance);

            // 지정된 targetRenderer(Sphere)의 머티리얼을 '허공에 있을 때' 용으로 변경합니다.
            targetRenderer.material = offHitMaterial;

            // 디버깅을 위해 허공을 보고 있음을 콘솔에 출력합니다.
            Debug.Log("허공을 바라보는 중...");
        }
    }
}