using System.Collections.Generic;
using UnityEngine;

public class PlasticbagSpawner : MonoBehaviour
{
    public GameObject prefabToSpawn;

    [Header("Spawn Settings")]
    public Vector3 spawnAreaCenter = Vector3.zero;
    public float spawnRadius = 1.5f;
    public float spawnHeightY = 1.5f;

    [Header("수량 제한")]
    [Tooltip("동시에 존재할 수 있는 최대 봉지 수")]
    public int maxBags = 10;

    [Header("스폰 간격")]
    [Tooltip("봉지 생성 간격 (초)")]
    public float spawnInterval = 5f;

    // 현재 살아있는 봉지 목록
    private List<GameObject> _activeBags = new List<GameObject>();
    private float _nextSpawnTime;

    void Start()
    {
        // 첫 봉지 즉시 생성
        SpawnPlasticbag();
        _nextSpawnTime = Time.time + spawnInterval;
    }

    void Update()
    {
        // 5초마다 최대 수량까지 자동 생성
        if (Time.time >= _nextSpawnTime)
        {
            if (_activeBags.Count < maxBags)
            {
                SpawnPlasticbag();
            }
            _nextSpawnTime = Time.time + spawnInterval;
        }
    }

    // 봉지가 파괴될 때 호출 (바닥 충돌 또는 7회 터치)
    public void OnBagDestroyed(GameObject bag)
    {
        _activeBags.Remove(bag);
    }

    public void SpawnPlasticbag()
    {
        if (prefabToSpawn == null) return;
        // 최대 수량 체크
        if (_activeBags.Count >= maxBags) return;

        Vector3 randomOffsetFromSphere = Random.insideUnitSphere * spawnRadius;
        Vector3 spawnPosition = spawnAreaCenter + randomOffsetFromSphere;
        spawnPosition.y = spawnHeightY;

        GameObject newBag = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        _activeBags.Add(newBag);

        PlasticBag bagScript = newBag.GetComponent<PlasticBag>();
        if (bagScript != null)
        {
            bagScript.spawner = this;
        }
    }
}
