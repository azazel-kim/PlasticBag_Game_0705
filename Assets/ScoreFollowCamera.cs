using UnityEngine;

// 점수 UI가 시야 전면 오른쪽 위에 부드럽게 고정되는 스크립트
// 머리를 돌려도 시야 밖으로 나가지 않지만, 직접 붙어있지 않아 자연스러움
public class ScoreFollowCamera : MonoBehaviour
{
    [Header("카메라 기준 오프셋")]
    [Tooltip("X: 오른쪽, Y: 위, Z: 앞")]
    public Vector3 offset = new Vector3(0.20f, 0.12f, 0.6f);

    [Header("따라가는 속도")]
    [Range(1f, 10f)]
    public float followSpeed = 3f;

    [Range(1f, 10f)]
    public float rotationSpeed = 3f;

    private Transform _cam;

    void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;

        // 즉시 목표 위치로 이동 (초기 위치 점프 방지)
        if (_cam != null)
        {
            transform.position = GetTargetPosition();
            transform.rotation = GetTargetRotation();
        }
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            if (_cam == null) return;
        }

        // 부드럽게 목표 위치/회전으로 이동
        transform.position = Vector3.Lerp(transform.position, GetTargetPosition(), Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, GetTargetRotation(), Time.deltaTime * rotationSpeed);
    }

    private Vector3 GetTargetPosition()
    {
        return _cam.position
            + _cam.right * offset.x
            + _cam.up * offset.y
            + _cam.forward * offset.z;
    }

    private Quaternion GetTargetRotation()
    {
        // 카메라와 같은 방향을 바라봄
        return _cam.rotation;
    }
}
