using UnityEngine;

public class PlasticBag : MonoBehaviour
{
    // 이 비닐봉지를 생성한 스포너를 기억하기 위한 변수
    public PlasticbagSpawner spawner;

    // 이 오브젝트(비닐봉지)가 다른 Collider와 충돌했을 때 호출됩니다.
    void OnCollisionEnter(Collision collision)
    {
        // 충돌한 상대방 오브젝트의 태그가 "Ground"인지 확인합니다.
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 1. 스포너에게 다음 비닐봉지를 생성하라고 요청합니다.
            // spawner 변수가 할당되지 않았을 경우를 대비해 null 체크를 해주는 것이 안전합니다.
            if (spawner != null)
            {
                spawner.SpawnPlasticbag();
            }

            // 2. 자기 자신(이 스크립트가 붙어있는 비닐봉지)을 파괴합니다.
            Destroy(gameObject);
        }
    }
}