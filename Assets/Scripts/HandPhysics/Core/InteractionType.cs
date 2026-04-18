namespace HandPhysics.Core
{
    /// <summary>
    /// 지원하는 인터랙션 타입.
    /// 각 타입은 서로 다른 물리 반응과 제스처 조건을 가집니다.
    /// </summary>
    public enum InteractionType
    {
        /// <summary>손으로 쳐서 튕기기 (플라스틱 백)</summary>
        Bounce,

        /// <summary>양손/한손으로 쥐어짜서 터트리기 (풍선)</summary>
        Squeeze,

        /// <summary>잡아서 바구니에 담기</summary>
        GrabPlace
    }

    /// <summary>
    /// 왼손/오른손 구분.
    /// Unity XR Hands의 Handedness와 호환되지만 독립적으로 정의하여
    /// HPTK 연동 시에도 사용 가능하게 합니다.
    /// </summary>
    public enum HandSide
    {
        Left,
        Right
    }
}
