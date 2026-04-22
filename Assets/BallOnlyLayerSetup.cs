using UnityEngine;

/// <summary>
/// 런타임에 "BallOnly" 레이어의 충돌 매트릭스를 구성합니다.
///  - BallOnly vs BallOnly: 활성 (공-공 바운스)
///  - BallOnly vs 그 외 모든 레이어: 비활성 (손/지면/상자 등 무시)
///
/// Ball prefab의 자식 GameObject "BallOnlyCollider"가 이 레이어에 소속되어
/// 공과 공 사이의 물리 충돌만 담당하고 손/지면과는 상호작용하지 않습니다.
///
/// 씬에 아무 GameObject 1개에만 붙여도 됨 (RuntimeInitializeOnLoadMethod로 1회 실행).
/// </summary>
public static class BallOnlyLayerSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        int bbo = LayerMask.NameToLayer("BallOnly");
        if (bbo < 0)
        {
            Debug.LogWarning("[BallOnlyLayer] 레이어 'BallOnly'를 찾을 수 없음. Physics matrix 구성 스킵.");
            return;
        }

        // 자기 자신 외 모든 32개 레이어와의 충돌 비활성
        for (int i = 0; i < 32; i++)
        {
            if (i == bbo) continue;
            Physics.IgnoreLayerCollision(bbo, i, true);
        }
        // 자기 자신끼리는 활성 (기본값이지만 명시)
        Physics.IgnoreLayerCollision(bbo, bbo, false);

        Debug.Log($"[BallOnlyLayer] 구성 완료: BallOnly(layer={bbo}) vs BallOnly만 충돌.");
    }
}
