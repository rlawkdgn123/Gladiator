using System;
using UnityEngine;
using UnityEngine.UI;

// 공용 가드 인디케이터 시스템
// - 커서 방향 읽기
// - 각 인디케이터의 색상/알파 갱신


namespace GuardIndicatorSystemInfo
{
    public enum IndicatorHealthState : int
    {
        Full = 0,   // 75 ~ 100% (Default)
        Damaged,     // 35 ~ 74%
        Critical,   // 1 ~ 34%
        Destroyed,  // 0%
    }

    public enum IndicatorActionState : int
    {
        None = 0, // Default
        Attack,
        Parry,
    }

    [Serializable]
    public class Components
    {
        [Header("GuardIndicator")]
        public GuardIndicatorBase[] GuardIndicators;

        [Space]
        [Header("OtherComponents")]
        public CursorManager CursorManager;
    }

    [Serializable]
    public class CheckOption
    {

        [Header("Offset")]
        public Vector3 IndicatorOffset;

        [Header("Alpha")]
        [Range(0f, 1f), Tooltip("비활성 인디케이터 알파.")]
        public float OffAlpha = 0.3f;
    }

    [Serializable]
    public class CurrentState
    {
        [Header("가드존.(GuardZone)")]
        public GuardZone GuardZone = GuardZone.Left;

        [Header("인디케이터 X 반전 (Left - Right)")]
        public bool IndicatorFlipX = false;
    }

    [Serializable]
    public class CurrentValue
    {

    }

}

public class GaurdIndicatorSystemBase : MonoBehaviour
{
    [SerializeField] protected GuardIndicatorSystemInfo.Components m_components = new();
    [SerializeField] protected GuardIndicatorSystemInfo.CheckOption m_checkOption = new();
    [SerializeField] protected GuardIndicatorSystemInfo.CurrentState m_currentState = new();

    protected GuardIndicatorSystemInfo.Components Components => m_components;
    protected GuardIndicatorSystemInfo.CheckOption CheckOptions => m_checkOption;
    protected GuardIndicatorSystemInfo.CurrentState States => m_currentState;

    private RectTransform m_canvasRectTransform;

    protected virtual void Awake()
    {
        Components.GuardIndicators = FindIndicators();
    }

    protected virtual void Start()
    {
        if (!Components.CursorManager)
            Components.CursorManager = CursorManager.Instance;

        if (!Components.CursorManager)
            Debug.LogError("[CursorManager] CursorManager 할당이 되지 않았습니다.");
    }

    private void Update()
    {
        GuardIndicatorStateUpdate();
    }

    protected virtual GuardIndicatorBase[] FindIndicators()
    {
        return GetComponentsInChildren<GuardIndicatorBase>();
    }

    protected void SetOpacity(Image image, float opacity)
    {
        Color color = image.color;
        color.a = opacity;
        image.color = color;
    }

    protected virtual void GuardIndicatorStateUpdate()
    {

    }
}
