using System.Collections.Generic;
using UnityEngine;

public class PlasticbagSpawner : MonoBehaviour
{
    public GameObject prefabToSpawn;

    [Header("Ball 혼합 스폰")]
    [Tooltip("공 프리팹(선택). null이면 플라스틱백만 스폰.")]
    public GameObject ballPrefab;

    [Tooltip("공이 스폰될 확률 (0=봉지만, 1=공만). 기본 0.3 = 30%가 공")]
    [Range(0f, 1f)]
    public float ballSpawnRatio = 0.3f;

    [Header("Spawn Settings")]
    public Vector3 spawnAreaCenter = Vector3.zero;
    public float spawnRadius = 1.5f;
    public float spawnHeightY = 1.5f;

    [Header("수량 제한 (봉지/공 분리 + 총합)")]
    [Tooltip("동시에 존재할 수 있는 최대 봉지 수")]
    public int maxBags = 15;

    [Tooltip("동시에 존재할 수 있는 최대 공 수")]
    public int maxBalls = 10;

    [Tooltip("봉지+공 합쳐서 공간 내 최대 동시 존재 수")]
    public int totalMax = 20;

    [Header("스폰 간격")]
    [Tooltip("봉지 생성 간격 (초)")]
    public float spawnInterval = 5f;

    // 현재 살아있는 오브젝트 목록 (봉지/공 분리 추적)
    private List<GameObject> _activeBags = new List<GameObject>();
    private List<GameObject> _activeBalls = new List<GameObject>();
    private float _nextSpawnTime;

    // GameFeelDebugPanel에서 "게임 시작" 누를 때까지 대기
    private bool _spawningEnabled = false;

    [Header("자동 시작")]
    [Tooltip("true면 씬 로드 시 자동으로 스폰 시작 (GameFeel 패널 없이)")]
    public bool autoStart = true;

    [Tooltip("자동 시작 시 딜레이 (초)")]
    public float autoStartDelay = 2f;

    void Start()
    {
        if (autoStart)
        {
            Invoke(nameof(StartSpawning), autoStartDelay);
            Debug.Log($"[Spawner] {autoStartDelay}초 후 자동 스폰 시작.");
        }
        else
        {
            Debug.Log("[Spawner] 대기 중 — StartSpawning() 호출을 기다립니다.");
        }
    }

    /// <summary>
    /// GameFeelDebugPanel에서 호출. 봉지 스폰을 시작합니다.
    /// </summary>
    public void StartSpawning()
    {
        _spawningEnabled = true;

        // 첫 봉지 즉시 생성
        SpawnPlasticbag();
        _nextSpawnTime = Time.time + spawnInterval;

        Debug.Log("[Spawner] 스폰 시작!");
    }

    void Update()
    {
        if (!_spawningEnabled) return;

        // 스폰 간격마다 각 타입별 최대 수량까지 자동 생성
        if (Time.time >= _nextSpawnTime)
        {
            if (_activeBags.Count < maxBags || _activeBalls.Count < maxBalls)
            {
                SpawnPlasticbag();
            }
            _nextSpawnTime = Time.time + spawnInterval;
        }
    }

    // 봉지/공이 파괴될 때 호출
    public void OnBagDestroyed(GameObject obj)
    {
        _activeBags.Remove(obj);
        _activeBalls.Remove(obj);
    }

    public void SpawnPlasticbag()
    {
        if (prefabToSpawn == null) return;

        // 총합 제한 먼저 확인
        int totalActive = _activeBags.Count + _activeBalls.Count;
        if (totalActive >= totalMax) return;

        // 타입별 빈자리 계산
        bool canSpawnBag = _activeBags.Count < maxBags;
        bool canSpawnBall = (ballPrefab != null) && (_activeBalls.Count < maxBalls);
        if (!canSpawnBag && !canSpawnBall) return;

        // 스폰 타입 결정: 한쪽만 가능하면 그쪽, 둘 다 가능하면 확률로
        bool spawnBall;
        if (canSpawnBag && !canSpawnBall) spawnBall = false;
        else if (!canSpawnBag && canSpawnBall) spawnBall = true;
        else spawnBall = Random.value < ballSpawnRatio;

        GameObject chosenPrefab = spawnBall ? ballPrefab : prefabToSpawn;

        Vector3 randomOffsetFromSphere = Random.insideUnitSphere * spawnRadius;
        Vector3 spawnPosition = spawnAreaCenter + randomOffsetFromSphere;

        // 헤드셋(카메라) 높이 기준 +25cm 위에서 스폰
        Camera cam = Camera.main;
        if (cam != null)
            spawnPosition.y = cam.transform.position.y + 0.25f;
        else
            spawnPosition.y = spawnHeightY;

        GameObject newObj = Instantiate(chosenPrefab, spawnPosition, Quaternion.identity);
        if (spawnBall)
        {
            _activeBalls.Add(newObj);
            // 공이 Ground/BoxFloor 접촉 시 자기 파괴 + 스포너 알림
            var limiter = newObj.GetComponent<BallBounceLimiter>();
            if (limiter != null) limiter.spawner = this;
        }
        else _activeBags.Add(newObj);

        // 플라스틱백인 경우만 기존 연동
        if (!spawnBall)
        {
            PlasticBag bagScript = newObj.GetComponent<PlasticBag>();
            if (bagScript != null)
            {
                bagScript.spawner = this;
            }

            // 새 봉지에 GameFeel 프리셋 적용
            if (GameFeelController.Instance != null)
            {
                GameFeelController.Instance.RefreshTargets();
                GameFeelController.Instance.ApplyPreset(GameFeelController.Instance.currentPresetIndex);
            }
        }
    }
}
