// HandTriggerSceneLoader.cs - 손으로 오브젝트를 터치하면 씬을 전환하는 스크립트
// XRI의 hover/select 대신 Poke Interactor 위치를 직접 거리 체크하여 터치를 감지합니다.
// 추가로 XRHandSubsystem의 검지 끝 관절 위치도 체크하여 이중 안전장치를 둡니다.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class HandTriggerSceneLoader : MonoBehaviour
{
    [Header("씬 설정")]
    [Tooltip("터치 시 이동할 씬 이름 (Build Settings에 추가되어 있어야 합니다)")]
    public string sceneToLoad = "3-1_PlasticBagPlay_with_Analyze";

    [Tooltip("터치 후 씬 전환까지 대기 시간 (초)")]
    public float delayBeforeLoad = 0.3f;

    [Header("터치 감지 설정")]
    [Tooltip("Collider bounds를 이만큼 확장하여 감지 영역을 넓힘 (미터)")]
    public float boundsExpansion = 0.05f;

    [Header("페이드 효과")]
    [Tooltip("화면 페이드용 CanvasGroup (없으면 바로 전환)")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("오디오")]
    [Tooltip("터치 시 재생할 사운드")]
    public AudioClip touchSound;
    [Range(0, 1)] public float touchVolume = 1.0f;

    [Header("시각 피드백")]
    [Tooltip("터치 시 색상 변경할 Renderer (없으면 자동 검색)")]
    public Renderer targetRenderer;
    public Color highlightColor = Color.green;

    [Header("디버그")]
    public bool showDebugMessages = true;

    // 중복 트리거 방지
    private bool _triggered = false;
    private Color _originalColor;
    private AudioSource _audioSource;
    private Collider _collider;
    private XRHandSubsystem _handSubsystem;

    // Start에서 캐싱 — 매 프레임 FindObjectsOfType 호출 방지
    private XRPokeInteractor[] _cachedPokeInteractors;
    private float _pokeInteractorCacheTime;
    private const float PokeInteractorCacheInterval = 2f; // 2초마다 갱신

    // Hand Joint → 월드 좌표 변환용 XR Origin
    private Transform _xrOriginTransform;

    void Start()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
            Log("Collider를 Trigger로 자동 설정했습니다.");
        }

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
            _originalColor = targetRenderer.material.color;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && touchSound != null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (string.IsNullOrEmpty(sceneToLoad))
            Debug.LogWarning($"[HandTrigger] {gameObject.name}: 이동할 씬 이름이 비어있습니다!");

        // Poke Interactor 캐싱
        RefreshPokeInteractorCache();

        // XR Origin Transform 캐싱 — hand joint 좌표를 월드 좌표로 변환할 때 필요
        var xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
        {
            _xrOriginTransform = xrOrigin.CameraFloorOffsetObject != null
                ? xrOrigin.CameraFloorOffsetObject.transform
                : xrOrigin.transform;
            Log($"XR Origin 찾음: {xrOrigin.name}, offset: {_xrOriginTransform.name}");
        }
        else
        {
            Log("XR Origin을 찾지 못함 — hand joint 좌표를 월드 좌표로 사용합니다.");
        }

        Log($"시작됨. Collider bounds: {_collider.bounds.center}, size: {_collider.bounds.size}");
    }

    // Poke Interactor 목록을 주기적으로 갱신 (씬 중간에 생성/삭제될 수 있으므로)
    private void RefreshPokeInteractorCache()
    {
        _cachedPokeInteractors = FindObjectsOfType<XRPokeInteractor>();
        _pokeInteractorCacheTime = Time.time;
        Log($"Poke Interactor 캐시 갱신: {_cachedPokeInteractors.Length}개");
    }

    void Update()
    {
        if (_triggered) return;

        // Poke Interactor 캐시 주기적 갱신
        if (Time.time - _pokeInteractorCacheTime > PokeInteractorCacheInterval)
            RefreshPokeInteractorCache();

        // 방법 1: XRPokeInteractor 위치로 직접 충돌 체크
        if (CheckPokeInteractorPositions()) return;

        // 방법 2: XRHandSubsystem 검지 끝 관절 위치로 직접 충돌 체크
        CheckHandJointPositions();
    }

    // Poke Interactor의 월드 위치가 이 오브젝트의 Collider 안에 있는지 확인
    private bool CheckPokeInteractorPositions()
    {
        if (_cachedPokeInteractors == null) return false;

        foreach (var poke in _cachedPokeInteractors)
        {
            if (poke == null || !poke.enabled) continue;

            Vector3 pokePos = poke.transform.position;
            if (IsInsideBounds(pokePos))
            {
                Log($"Poke Interactor 터치 감지! {poke.gameObject.name} at {pokePos}");
                TriggerSceneTransition($"PokeInteractor: {poke.gameObject.name}");
                return true;
            }
        }
        return false;
    }

    // Hand Joint의 로컬 좌표를 월드 좌표로 변환
    private Vector3 JointPoseToWorldPosition(Pose localPose)
    {
        if (_xrOriginTransform != null)
            return _xrOriginTransform.TransformPoint(localPose.position);

        // XR Origin이 없으면 그대로 월드 좌표로 사용
        return localPose.position;
    }

    // XRHandSubsystem에서 직접 검지 끝 관절 위치를 가져와 충돌 체크
    private void CheckHandJointPositions()
    {
        if (_handSubsystem == null || !_handSubsystem.running)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
        }

        if (_handSubsystem == null) return;

        // 왼손 검지 끝
        if (_handSubsystem.leftHand.isTracked)
        {
            var joint = _handSubsystem.leftHand.GetJoint(XRHandJointID.IndexTip);
            if (joint.TryGetPose(out Pose pose))
            {
                Vector3 worldPos = JointPoseToWorldPosition(pose);
                if (IsInsideBounds(worldPos))
                {
                    Log($"왼손 검지 터치 감지! at {worldPos}");
                    TriggerSceneTransition("LeftHand IndexTip");
                    return;
                }
            }
        }

        // 오른손 검지 끝
        if (_handSubsystem.rightHand.isTracked)
        {
            var joint = _handSubsystem.rightHand.GetJoint(XRHandJointID.IndexTip);
            if (joint.TryGetPose(out Pose pose))
            {
                Vector3 worldPos = JointPoseToWorldPosition(pose);
                if (IsInsideBounds(worldPos))
                {
                    Log($"오른손 검지 터치 감지! at {worldPos}");
                    TriggerSceneTransition("RightHand IndexTip");
                    return;
                }
            }
        }
    }

    // 주어진 월드 좌표가 Collider 안에 있는지 (회전 반영, OBB 기반)
    // Collider.ClosestPoint로 가장 가까운 표면 점을 구한 뒤 거리로 판정
    private bool IsInsideBounds(Vector3 worldPoint)
    {
        if (_collider == null) return false;

        // ClosestPoint: 포인트가 콜라이더 안에 있으면 자기 자신을 반환
        Vector3 closest = _collider.ClosestPoint(worldPoint);
        float dist = Vector3.Distance(worldPoint, closest);

        // dist ≈ 0이면 콜라이더 내부, boundsExpansion 이내면 확장 영역 안
        return dist <= boundsExpansion;
    }

    // 기존 OnTriggerEnter도 유지 (물리 충돌 백업용)
    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        string name = other.gameObject.name.ToLower();
        if (name.Contains("hand") || name.Contains("poke") || name.Contains("direct"))
        {
            TriggerSceneTransition($"Trigger 충돌: {other.gameObject.name}");
        }
    }

    private void TriggerSceneTransition(string source)
    {
        if (_triggered) return;
        _triggered = true;

        Log($"손 터치 감지! ({source}) → '{sceneToLoad}' 씬으로 전환합니다.");

        if (targetRenderer != null)
            targetRenderer.material.color = highlightColor;

        if (_audioSource != null && touchSound != null)
            _audioSource.PlayOneShot(touchSound, touchVolume);

        StartCoroutine(LoadSceneRoutine());
    }

    IEnumerator LoadSceneRoutine()
    {
        if (delayBeforeLoad > 0)
            yield return new WaitForSeconds(delayBeforeLoad);

        if (string.IsNullOrEmpty(sceneToLoad)) yield break;

        // SceneFadeManager가 있으면 전역 페이드 사용
        if (SceneFadeManager.Instance != null)
        {
            Log($"SceneFadeManager로 씬 전환: {sceneToLoad}");
            SceneFadeManager.Instance.FadeToScene(sceneToLoad);
            yield break;
        }

        // SceneFadeManager가 없으면 기존 방식으로 페이드 아웃
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        Log($"씬 로드: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }

    void OnTriggerExit(Collider other)
    {
        if (!_triggered && targetRenderer != null)
            targetRenderer.material.color = _originalColor;
    }

    private void Log(string message)
    {
        if (showDebugMessages)
            Debug.Log($"[HandTrigger] {message}");
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
        }
    }
#endif
}
