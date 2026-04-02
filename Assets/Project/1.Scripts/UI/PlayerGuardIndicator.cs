/*
 * 플레이어 가드 인디케이터
 * - UI 플레이어 추적
 */

using UnityEngine;
using UnityEngine.UI;

public class PlayerGuardIndicator : MonoBehaviour
{
    [Header("OtherComponents")]
    [SerializeField] private Canvas m_Canvas;
    [SerializeField] private Camera m_Camera;

    [Header("IndicatorAnchor")]
    [SerializeField] private Transform m_IndicatorAnchor;
    [Tooltip("방향 표시기 중앙 위치값")]
    [SerializeField] private Vector3 m_IndicatorPos;

    [Header("UIOffset")]
    [SerializeField] private Vector3 m_TopOffset;
    [SerializeField] private Vector3 m_LeftOffset;
    [SerializeField] private Vector3 m_RightOffset;

    [Header("RectTransform")]
    [SerializeField] private RectTransform m_TopTrans;
    [SerializeField] private RectTransform m_LeftTrans;
    [SerializeField] private RectTransform m_RightTrans;

    private void Awake()
    {
        if (!m_IndicatorAnchor)
            Debug.LogError("[Indicator] IndicatorAnchor가 할당되지 않았습니다.", this);

        if (!m_TopTrans || !m_LeftTrans || !m_RightTrans)
            Debug.LogWarning("[Indicator] 할당되지 않은 방향 UI가 있습니다.", this);

        if (!m_IndicatorAnchor && transform.parent)
        {
            Transform uiAnchor = transform.parent.Find("UIAnchor");
            m_IndicatorAnchor = uiAnchor != null ? uiAnchor : transform.parent;
        }

        if (!m_Camera)
            m_Camera = Camera.main;

        if (!m_Camera)
            Debug.LogError("[Indicator] 카메라가 할당되지 않았습니다.", this);

        if (!m_Canvas)
            m_Canvas = GetComponent<Canvas>();

        if (m_Canvas)
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    private void Update()
    {
        // 포지션 갱신
        m_IndicatorPos = m_IndicatorAnchor.position;

        // 인디케이터 업데이트
        if (m_TopTrans)
            RefreshIndicator(m_IndicatorPos, m_TopOffset, m_TopTrans);
        if (m_LeftTrans)
            RefreshIndicator(m_IndicatorPos, m_LeftOffset, m_LeftTrans);
        if (m_RightTrans)
            RefreshIndicator(m_IndicatorPos, m_RightOffset, m_RightTrans);
    }

    void RefreshIndicator(Vector3 worldPos, Vector3 worldOffset, RectTransform UITrans) {
        if (!m_Camera || !UITrans)
            return;

        Vector3 worldPoint = worldPos + worldOffset;
        Vector3 screenPoint = m_Camera.WorldToScreenPoint(worldPoint);

        if (screenPoint.z <= 0f)
            return;

        UITrans.position = screenPoint;
    }
}

