using UnityEngine;
using UnityEngine.UI;

// 플레이어 전용 가드 인디케이터 시스템
// - 공용 시스템을 상속
// - 플레이어 참조만 연결
public class PlayerGuardIndicatorSystem : GaurdIndicatorSystemBase
{
    [SerializeField] private PlayerController m_playerController;

    protected override void Awake()
    {
        base.Awake();

        if (!m_playerController)
            m_playerController = GetComponentInParent<PlayerController>();

        if (!m_playerController)
            Debug.LogError("[PlayerGuardIndicatorSystem] PlayerController 할당이 되지 않았습니다.");
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override GuardIndicatorBase[] FindIndicators()
    {
        return GetComponentsInChildren<PlayerGuardIndicator>();
    }

    protected override void GuardIndicatorStateUpdate()
    {
        CursorManager cursorManager = Components.CursorManager != null ? Components.CursorManager : CursorManager.Instance;
        if (cursorManager == null || Components.GuardIndicators == null)
            return;

        for (int i = 0; i < Components.GuardIndicators.Length; ++i)
        {
            GuardIndicatorBase indicator = Components.GuardIndicators[i];
            if (indicator == null)
                continue;

            Image image = indicator.GetComponent<Image>();
            if (image == null)
                continue;

            switch (indicator.GetHealthState())
            {
                case GuardIndicatorInfo.IndicatorHealthState.Critical: image.color = Color.orangeRed; break;
                case GuardIndicatorInfo.IndicatorHealthState.Destroyed: image.color = Color.black; break;
                default: image.color = Color.green; break;
            }

            switch (indicator.GetActionState())
            {
                case GuardIndicatorInfo.IndicatorActionState.Attack: image.color = Color.darkRed; break;
                case GuardIndicatorInfo.IndicatorActionState.Parry: image.color = Color.orangeRed; break;
                default: break;
            }

            if (cursorManager.GetGuardZoneDirection() == indicator.GetGuardZone())
                SetOpacity(image, 1f);
            else
                SetOpacity(image, CheckOptions.OffAlpha);
        }
    }
}
