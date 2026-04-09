using UnityEngine;

public class PlasticBag : MonoBehaviour
{
    // 이 비닐봉지를 생성한 스포너를 기억하기 위한 변수
    public PlasticbagSpawner spawner;

    // 이 오브젝트(비닐봉지)가 다른 Collider와 충돌했을 때 호출됩니다.
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 스포너에게 파괴를 알림 → 스포너가 새 봉지 생성 판단
            if (spawner != null)
            {
                spawner.OnBagDestroyed(gameObject);
            }

            Destroy(gameObject);
        }
    }
}