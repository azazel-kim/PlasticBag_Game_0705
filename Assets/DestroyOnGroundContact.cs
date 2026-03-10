using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyOnGroundContact : MonoBehaviour
{
    // 이 스크립트가 붙은 오브젝트가 다른 Collider와 충돌했을 때 호출됩니다.
    void OnCollisionEnter(Collision collision)
    {
        // 충돌한 상대방 오브젝트의 태그가 "Ground"인지 확인합니다.
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 이 게임 오브젝트(즉, 플라스틱 백 자신)를 파괴합니다.
            Destroy(gameObject);
        }
    }
}
