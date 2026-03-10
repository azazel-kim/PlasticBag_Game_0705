using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public float lifetime = 5.0f; // 프리팹이 살아있을 시간 (초)

    void Start()
    {
        // lifetime 초 후에 DestroySelf 함수를 호출하도록 예약합니다.
        Invoke("DestroySelf", lifetime);
    }

    void DestroySelf()
    {
        // 이 스크립트가 붙어있는 게임 오브젝트를 파괴합니다.
        Destroy(gameObject);
    }
}
