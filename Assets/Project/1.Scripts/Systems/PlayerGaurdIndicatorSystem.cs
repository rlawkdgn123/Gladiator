using UnityEngine;
using UnityEngine.UI;

public class PlayerGaurdIndicatorSystem : MonoBehaviour
{
    [SerializeField] private PlayerGuardIndicator[] m_playerGuardIndicators;
    [SerializeField] private Image[] m_playerGuardIndicatorImages;
    [SerializeField] private PlayerController m_playerController;
    [SerializeField] private CursorManager m_cursorManager;

    [Range(0f, 1f), Tooltip("기본 달리기 속도.")]
    [SerializeField] private float m_OffAlpha = 0.3f;

    private void Awake()
    {
        m_playerGuardIndicators = GetComponentsInChildren<PlayerGuardIndicator>();
        m_playerGuardIndicatorImages = GetComponentsInChildren<Image>();

        // 플레이어 컨트롤러
        if (!m_playerController)
            m_playerController =  GetComponentInParent<PlayerController>();
        if (!m_playerController) Debug.LogError("[PlayerGaurdIndicatorSystem] PlayerController 할당이 되지 않았습니다.");


        // 커서매니저
        if (!m_cursorManager)
            m_cursorManager = GameObject.Find("CursorManager").GetComponent<CursorManager>();

        if (!m_cursorManager) Debug.LogError("[CursorManager] CursorManager 할당이 되지 않았습니다.");
    }

    private void Start()
    {
        
    }

    private void Update()
    {
        GuardIndicatorStateUpdate();
    }

    // 투명도 조절
    private void SetOpacity(Image image, float opacity)
    {
        Color color = image.color;
        color.a = opacity;
        image.color = color;
    }

    void GuardIndicatorStateUpdate()
    {

        for (int i = 0; i < m_playerGuardIndicators.Length; ++i)
        {
            PlayerGuardIndicator indicator = m_playerGuardIndicators[i];
            Image image = m_playerGuardIndicatorImages[i];

            switch (indicator.GetHealthState())
            {
                case GuardIndicator.IndicatorHealthState.Weak:  image.color = Color.orangeRed; break;
                case GuardIndicator.IndicatorHealthState.Break: image.color = Color.black; break;
                default: image.color = Color.green; break;
            }

            switch (indicator.GetActionState())
            {
                case GuardIndicator.IndicatorActionState.Attack: image.color = Color.darkRed; break;
                case GuardIndicator.IndicatorActionState.Parry:  image.color = Color.orangeRed; break;
                default: break;
            }

            // 주의 : 커서매니저의 GuardZone() : 현재 마우스에 따른 위치
            // 주의 : 인디케이터의 GuardZone() : 사전 설정된 값 (스스로 표기용)
            if (CursorManager.Instance.GetGuardZone() == indicator.GetGuardZone()) // 현재 값이 같다면
                SetOpacity(image, 1f);
            else
                SetOpacity(image, m_OffAlpha);
        }
    }
}
