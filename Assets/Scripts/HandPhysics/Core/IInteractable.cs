namespace HandPhysics.Core
{
    /// <summary>
    /// 손-오브젝트 접촉 정보를 담는 구조체.
    /// HandContactDetector가 생성하여 IInteractable에 전달합니다.
    /// </summary>
    public struct ContactInfo
    {
        /// <summary>접촉한 손 (왼손/오른손)</summary>
        public HandSide HandSide;

        /// <summary>접촉 지점 (월드 좌표)</summary>
        public UnityEngine.Vector3 ContactPoint;

        /// <summary>접촉 방향 (법선)</summary>
        public UnityEngine.Vector3 ContactNormal;

        /// <summary>접촉 시 상대 속도 크기</summary>
        public float RelativeSpeed;

        /// <summary>접촉한 손 부위 (Palm, ThumbTip, IndexTip 등)</summary>
        public HandPartType HandPart;
    }

    /// <summary>
    /// 접촉한 손 부위 분류.
    /// 어떤 부위로 접촉했는지에 따라 인터랙션 반응이 달라질 수 있습니다.
    /// </summary>
    public enum HandPartType
    {
        Palm,
        ThumbTip,
        IndexTip,
        MiddleTip,
        RingTip,
        PinkyTip,
        Other
    }

    /// <summary>
    /// 물리 기반 손 인터랙션의 공통 인터페이스.
    ///
    /// 모든 인터랙터블 오브젝트(플라스틱 백, 풍선, 잡기 대상)가 구현합니다.
    /// HandContactDetector가 접촉 상태 변화를 감지하면 해당 메서드를 호출합니다.
    ///
    /// 양손 인터랙션:
    /// - OnHandGrasp/OnHandUngrasp는 각 손마다 독립적으로 호출됩니다.
    /// - 양손이 동시에 Grasped 상태가 되면 OnBimanualGrasp가 추가로 호출됩니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>이 오브젝트의 인터랙션 타입</summary>
        InteractionType Type { get; }

        /// <summary>손이 감지 범위에 진입했을 때</summary>
        void OnHandEnter(ContactInfo info);

        /// <summary>손이 오브젝트에 물리적으로 접촉했을 때</summary>
        void OnHandTouch(ContactInfo info);

        /// <summary>손이 오브젝트를 쥐고 있을 때 (2개 이상 파트 + special part 접촉)</summary>
        void OnHandGrasp(ContactInfo info);

        /// <summary>쥐기를 해제했을 때</summary>
        void OnHandUngrasp(ContactInfo info);

        /// <summary>손이 감지 범위를 벗어났을 때</summary>
        void OnHandExit(ContactInfo info);

        /// <summary>양손이 동시에 오브젝트를 쥐었을 때</summary>
        void OnBimanualGrasp(ContactInfo leftInfo, ContactInfo rightInfo);

        /// <summary>양손 쥐기가 해제되었을 때 (한 손이라도 놓으면 호출)</summary>
        void OnBimanualUngrasp(HandSide releasedSide);
    }
}
