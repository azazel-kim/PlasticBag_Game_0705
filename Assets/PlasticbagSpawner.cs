using UnityEngine;

public class PlasticbagSpawner : MonoBehaviour
{
    // 어떤 프리팹을 생성할지 지정하는 변수는 그대로 유지합니다.
    public GameObject prefabToSpawn;

    [Header("Spawn Settings")]
    public Vector3 spawnAreaCenter = Vector3.zero;
    public float spawnRadius = 1.5f;
    public float spawnHeightY = 1.5f;

    // 타이머 관련 변수들은 더 이상 필요 없으므로 삭제합니다.

    void Start()
    {
        // 게임이 시작되자마자 첫 번째 비닐봉지를 생성합니다.
        SpawnPlasticbag();
    }

    // Update 함수는 더 이상 필요 없으므로 삭제합니다.

    // OnCollisionEnter 함수도 스포너의 역할이 아니므로 삭제합니다.

    // SpawnPlasticbag 함수를 수정하여, 생성된 봉지에게 이 스크립트의 정보를 넘겨줍니다.
    public void SpawnPlasticbag()
    {
        Vector3 randomOffsetFromSphere = Random.insideUnitSphere * spawnRadius;
        Vector3 spawnPosition = spawnAreaCenter + randomOffsetFromSphere;
        spawnPosition.y = spawnHeightY;

        // 1. 프리팹을 생성하고, 생성된 인스턴스를 'newBag' 변수에 저장합니다.
        GameObject newBag = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        // 2. 생성된 'newBag' 오브젝트에서 'PlasticBag' 스크립트 컴포넌트를 찾습니다.
        PlasticBag bagScript = newBag.GetComponent<PlasticBag>();

        // 3. 만약 스크립트를 찾았다면, 그 스크립트의 'spawner' 변수에 자기 자신(this)을 할당합니다.
        if (bagScript != null)
        {
            bagScript.spawner = this;
        }
    }
}