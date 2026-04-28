using UnityEngine;
using UnityEngine.UI;

// 공용 가드 인디케이터 시스템
// - 커서 방향 읽기
// - 각 인디케이터의 색상/알파 갱신
public class GaurdIndicatorSystemBase : MonoBehaviour
{
    [SerializeField] private GuardIndicator[] m_guardIndicators;
    [SerializeField] private CursorManager m_cursorManager;

    [Range(0f, 1f), Tooltip("비활성 인디케이터 알파.")]
    [SerializeField] private float m_OffAlpha = 0.3f;

    protected virtual void Awake()
    {
        m_guardIndicators = FindIndicators();
    }

    protected virtual void Start()
    {
        if (!m_cursorManager)
            m_cursorManager = CursorManager.Instance;

        if (!m_cursorManager)
            Debug.LogError("[CursorManager] CursorManager 할당이 되지 않았습니다.");
    }

    private void Update()
    {
        GuardIndicatorStateUpdate();
    }

    protected virtual GuardIndicator[] FindIndicators()
    {
        return GetComponentsInChildren<GuardIndicator>();
    }

    private void SetOpacity(Image image, float opacity)
    {
        Color color = image.color;
        color.a = opacity;
        image.color = color;
    }

    private void GuardIndicatorStateUpdate()
    {
        CursorManager cursorManager = m_cursorManager != null ? m_cursorManager : CursorManager.Instance;
        if (cursorManager == null || m_guardIndicators == null)
            return;

        for (int i = 0; i < m_guardIndicators.Length; ++i)
        {
            GuardIndicator indicator = m_guardIndicators[i];
            if (indicator == null)
                continue;

            Image image = indicator.GetComponent<Image>();
            if (image == null)
                continue;

            switch (indicator.GetHealthState())
            {
                case GuardIndicatorHUD.IndicatorHealthState.Critical: image.color = Color.orangeRed; break;
                case GuardIndicatorHUD.IndicatorHealthState.Destroyed: image.color = Color.black; break;
                default: image.color = Color.green; break;
            }

            switch (indicator.GetActionState())
            {
                case GuardIndicatorHUD.IndicatorActionState.Attack: image.color = Color.darkRed; break;
                case GuardIndicatorHUD.IndicatorActionState.Parry: image.color = Color.orangeRed; break;
                default: break;
            }

            if (cursorManager.GetGuardZoneDirection() == indicator.GetGuardZone())
                SetOpacity(image, 1f);
            else
                SetOpacity(image, m_OffAlpha);
        }
    }
}
