using UnityEngine;

// 플레이어 전용 가드 인디케이터 시스템
// - 공용 시스템을 상속
// - 플레이어 참조만 연결
public class PlayerGaurdIndicatorSystem : GaurdIndicatorSystemBase
{
    [SerializeField] private PlayerController m_playerController;

    protected override void Awake()
    {
        base.Awake();

        if (!m_playerController)
            m_playerController = GetComponentInParent<PlayerController>();

        if (!m_playerController)
            Debug.LogError("[PlayerGaurdIndicatorSystem] PlayerController 할당이 되지 않았습니다.");
    }

    protected override GuardIndicator[] FindIndicators()
    {
        return GetComponentsInChildren<PlayerGuardIndicator>();
    }
}
