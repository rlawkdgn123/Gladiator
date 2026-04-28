/*
 * 플레이어 가드 인디케이터
 * - 공용 GuardIndicator를 상속
 * - 플레이어 기준 참조만 연결
 */
using GuardIndicatorHUD;
using UnityEngine;


namespace PlayerGuardIndicatorHUD
{
    public class Components
    {
        [Header("GuardIndicator")]
        public RectTransform RectTransform;
        public Animator Animator;

        [Header("IndicatorAnchor")]
        public Transform IndicatorAnchor;

        [Space]
        [Header("IndicatorCanvas")]
        public Canvas IndicatorCanvas;

        [Space]
        [Header("OtherComponents")]
        public PlayerController PlayerController;
        public Camera MainCamera;
    }
}


public class PlayerGuardIndicator : GuardIndicator
{

    [SerializeField] private PlayerGuardIndicatorHUD.Components m_components = new();
    private PlayerGuardIndicatorHUD.Components Components => m_components;

    protected override void Awake()
    {
        if (!Components.PlayerController)
            Components.PlayerController = GetComponentInParent<PlayerController>();

        if (!Components.IndicatorAnchor && Components.PlayerController)
        {
            Transform uiAnchor = Components.PlayerController.transform.Find("IndicatorAnchor");
            Components.IndicatorAnchor = uiAnchor != null ? uiAnchor : transform.parent;
        }

        if (!Components.IndicatorAnchor)
            Debug.LogError("[Indicator] IndicatorAnchor가 할당되지 않았습니다.", this);

        base.Awake();
    }
}
