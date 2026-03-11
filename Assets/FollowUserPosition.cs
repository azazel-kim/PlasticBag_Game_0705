// FollowUserPosition.cs
// 씬 오브젝트를 사용자의 눈앞으로 배치합니다.
// XR Origin이 아닌 콘텐츠 오브젝트(Canvas, 게임 오브젝트 등)에 붙여 사용합니다.
using UnityEngine;

public class FollowUserPosition : MonoBehaviour
{
    [Tooltip("기준이 될 Main Camera (XR Origin > Camera Offset > Main Camera)")]
    public Transform headTransform;

    [Tooltip("사용자 앞 거리 (미터)")]
    public float distanceFromUser = 1.5f;

    [Tooltip("사용자 눈 높이 오프셋 (미터, 0이면 눈높이 그대로)")]
    public float heightOffset = -0.2f;

    [Tooltip("시작 시에만 위치를 잡고, 이후에는 고정할지 여부")]
    public bool positionOnceOnly = true;

    [Tooltip("오른쪽 컨트롤러 터치로 리셋 (Samsung XR 오른쪽 길게 터치)")]
    public bool enableManualReset = true;

    private bool _hasPositioned = false;

    void Start()
    {
        if (headTransform == null)
        {
            headTransform = Camera.main?.transform;
        }

        if (headTransform == null)
        {
            Debug.LogError("[FollowUserPosition] Main Camera를 찾을 수 없습니다.");
            this.enabled = false;
            return;
        }
    }

    void Update()
    {
        // 아직 위치를 잡지 않았으면 위치 설정
        if (!_hasPositioned)
        {
            // 카메라가 초기화될 때까지 약간 대기
            if (headTransform.position.sqrMagnitude > 0.001f || Time.frameCount > 30)
            {
                RepositionInFrontOfUser();
                _hasPositioned = true;
            }
        }

        // 수동 리셋: 키보드 R키 (에디터) 또는 런타임에서 호출 가능
        if (enableManualReset && !positionOnceOnly)
        {
            // Update에서 매 프레임 따라가기
            RepositionInFrontOfUser();
        }
    }

    /// <summary>
    /// 사용자 눈앞으로 오브젝트를 이동시킵니다.
    /// 외부에서 호출할 수도 있습니다.
    /// </summary>
    public void RepositionInFrontOfUser()
    {
        if (headTransform == null) return;

        // 카메라의 forward 방향 (Y축 회전만 사용하여 수평 유지)
        Vector3 flatForward = headTransform.forward;
        flatForward.y = 0;
        flatForward.Normalize();

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        // 사용자 앞 위치 계산
        Vector3 targetPos = headTransform.position + flatForward * distanceFromUser;
        targetPos.y = headTransform.position.y + heightOffset;

        transform.position = targetPos;

        // 사용자를 바라보도록 회전 (Y축만)
        Vector3 lookDir = flatForward;
        transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
    }
}
