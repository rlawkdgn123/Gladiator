/*
 * 공용 가드 인디케이터
 * - UI 본체 추적
 * - 캔버스 좌표 변환
 * - 상태/표시 데이터 보관
 */
using System;
using UnityEngine;

namespace GuardIndicatorHUD
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
        public RectTransform RectTransform;
        public Animator Animator;

        [Header("IndicatorAnchor")]
        public Transform IndicatorAnchor;

        [Space]
        [Header("IndicatorCanvas")]
        public Canvas IndicatorCanvas;

        [Space]
        [Header("OtherComponents")]
        public Camera MainCamera;
    }

    [Serializable]
    public class CheckOption
    {
        public CursorManager.GuardZone GuardZone;

        [Header("Offset")]
        public Vector3 IndicatorOffset;

        [Header("Direction")]
        [Tooltip("시작 및 기본 가드 방향")]
        public const int DefaultGuardDirection = (int)CursorManager.GuardZone.Right;
    }

    [Serializable]
    public class CurrentState
    {
        [Header("Direction")]
        [Tooltip("커서가 해당하는 존에 입성했는지")]
        public bool IsActive;

        [Space]
        [Header("State")]
        [Tooltip("체력에 따른 게이지 상태")]
        public IndicatorHealthState HealthState;
        [Tooltip("행동에 따른 게이지 상태")]
        public IndicatorActionState ActionState;
    }

    [Serializable]
    public class CurrentValue
    {
        [Header("Movement")]
        public float MoveAmount;
        public float TargetMoveAmount;
        public float MoveInputX;
        public float MoveInputZ;
        public Vector3 MoveDirection;
        public Vector3 DefaultMoveDirection;
        public Vector3 GroundNormal;
        public Vector3 GroundCross;
        public Vector3 PlayerVelocity;
    }

    [Serializable]
    public class Status
    {
        [Header("Health")]
        [Range(0f, 99999f), Tooltip("인디케이터 최대 체력.")]
        public float MaxHealth;
        [Range(1f, 99999f), Tooltip("인디케이터 최대 체력.")]
        public float CurrentHealth;
        [Range(1f, 99999f), Tooltip("인디케이터 체력 회복 속도 (Per Sec).")]
        public float HealthRecoveryRate;
    }

    [Serializable]
    public class ImageSet
    {
        [Header("Default")]
        [Tooltip("방향 표시기 중앙 위치값.")]
        public Sprite[] IndicatorSprites = new Sprite[3];
    }
}

public class GuardIndicator : MonoBehaviour
{

    [SerializeField] private GuardIndicatorHUD.Components m_components = new();
    [SerializeField] protected GuardIndicatorHUD.CheckOption m_checkOption = new();
    [SerializeField] protected GuardIndicatorHUD.CurrentState m_currentState = new();
    [SerializeField] protected GuardIndicatorHUD.ImageSet m_imageSet = new();
    [SerializeField] protected GuardIndicatorHUD.Status m_status = new();

    private GuardIndicatorHUD.Components Components => m_components;
    protected GuardIndicatorHUD.CheckOption CheckOptions => m_checkOption;
    protected GuardIndicatorHUD.CurrentState States => m_currentState;
    protected GuardIndicatorHUD.ImageSet Images => m_imageSet;
    protected GuardIndicatorHUD.Status Stats => m_status;

    private RectTransform m_canvasRectTransform;

    protected virtual void Awake()
    {
        if (!Components.RectTransform)
            Components.RectTransform = GetComponent<RectTransform>();

        if (!Components.MainCamera)
            Components.MainCamera = Camera.main;

        if (!Components.MainCamera)
            Debug.LogError("[Indicator] 카메라가 할당되지 않았습니다.", this);

        if (!Components.IndicatorCanvas)
            Components.IndicatorCanvas = GetComponentInParent<Canvas>();

        if (!Components.IndicatorCanvas)
            Debug.LogError("[Indicator] 캔버스가 할당되지 않았습니다.", this);

        if (Components.IndicatorCanvas)
            m_canvasRectTransform = Components.IndicatorCanvas.GetComponent<RectTransform>();
    }

    protected virtual void FixedUpdate()
    {
        if (!Components.IndicatorAnchor)
            return;

        RefreshIndicator(
            Components.IndicatorAnchor.position,
            CheckOptions.IndicatorOffset,
            Components.RectTransform);
    }


    protected void RefreshIndicator(Vector3 worldPos, Vector3 worldOffset, RectTransform uiTransform)
    {
        if (!Components.MainCamera || !uiTransform || m_canvasRectTransform == null)
            return;

        Transform camTransform = Components.MainCamera.transform;
        Vector3 worldPoint =
            worldPos
            + camTransform.up * worldOffset.y
            + camTransform.right * worldOffset.x
            + camTransform.forward * worldOffset.z;
        Vector3 screenPoint = Components.MainCamera.WorldToScreenPoint(worldPoint);

        if (screenPoint.z <= 0f)
            return;

        Camera uiCamera =
            Components.IndicatorCanvas != null &&
            Components.IndicatorCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? Components.IndicatorCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_canvasRectTransform,
            screenPoint,
            uiCamera,
            out Vector2 localPoint))
        {
            uiTransform.anchoredPosition = localPoint;
        }
    }

    public void SetGuardZone(CursorManager.GuardZone guardZone)
    {
        States.IsActive = (guardZone == CheckOptions.GuardZone);
    }
    public void SetActionState(GuardIndicatorHUD.IndicatorActionState actionState)
    {
        States.ActionState = actionState;
    }

    public void SetHealthState(GuardIndicatorHUD.IndicatorHealthState healthState)
    {
        States.HealthState = healthState;
    }

    public CursorManager.GuardZone GetGuardZone()
    {
        return CheckOptions.GuardZone;
    }

    public GuardIndicatorHUD.IndicatorActionState GetActionState()
    {
        return States.ActionState;
    }

    public GuardIndicatorHUD.IndicatorHealthState GetHealthState()
    {
        return States.HealthState;
    }
}
