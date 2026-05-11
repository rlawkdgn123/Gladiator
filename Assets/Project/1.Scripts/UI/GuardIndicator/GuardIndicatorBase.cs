/*
 * 가드 인디케이터 베이스
 * - UI 본체 추적
 * - 캔버스 좌표 변환
 * - 상태/표시 데이터 보관
 * - 반드시 부모가 "GuardIndicatorSystem" 종류의 스크립트를 가진 캔버스일 것.
 * - 반드시 본체(루트) 자식에 "IndicatorAnchor" 빈 오브젝트를 넣을 것.
 */
using System;
using UnityEngine;

namespace GuardIndicatorInfo
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
        public RectTransform RectTransform; // 본인
        public Animator Animator;

        [Header("IndicatorAnchor")]
        public Transform IndicatorAnchor;

        [Space]
        [Header("IndicatorCanvas")]
        public Canvas IndicatorCanvas;
        public RectTransform CanvasRectTransform; // 부모

        [Space]
        [Header("ResizeTarget")]
        public Transform ResizeTarget;

        [Space]
        [Header("OtherComponents")]
        public Camera MainCamera;

    }

    [Serializable]
    public class CheckOption
    {
        public GuardZone GuardZone;

        [Header("Offset")]
        public Vector3 IndicatorOffset;

        [Header("Direction")]
        [Tooltip("시작 및 기본 가드 방향")]
        public const int DefaultGuardDirection = (int)GuardZone.Right;

        [Header("Resize")]
        public float MinDistance = 2f;
        public float MaxDistance = 10f;
        public float MinScale = 0.6f;
        public float MaxScale = 1.4f;
        public Vector3 DefaultScale;
    }

    [Serializable]
    public class CurrentState
    {
        [Header("Resize")]
        public bool UseDistanceResize = false;

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

public class GuardIndicatorBase : MonoBehaviour
{

    [SerializeField] protected GuardIndicatorInfo.Components m_components = new();
    [SerializeField] protected GuardIndicatorInfo.CheckOption m_checkOption = new();
    [SerializeField] protected GuardIndicatorInfo.CurrentState m_currentState = new();
    [SerializeField] protected GuardIndicatorInfo.ImageSet m_imageSet = new();
    [SerializeField] protected GuardIndicatorInfo.Status m_status = new();

    protected GuardIndicatorInfo.Components Components => m_components;
    protected GuardIndicatorInfo.CheckOption CheckOptions => m_checkOption;
    protected GuardIndicatorInfo.CurrentState States => m_currentState;
    protected GuardIndicatorInfo.ImageSet Images => m_imageSet;
    protected GuardIndicatorInfo.Status Stats => m_status;

    protected virtual void Awake()
    {
        GameObject root = transform.root.gameObject;

        if (!Components.IndicatorAnchor && root)
        {
            Transform uiAnchor = root.transform.Find("IndicatorAnchor");
            Components.IndicatorAnchor = uiAnchor != null ? uiAnchor : transform.parent;
        }

        if (!Components.IndicatorAnchor)
            Debug.LogError("[Indicator] IndicatorAnchor가 할당되지 않았습니다.", this);

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

        if (!Components.CanvasRectTransform && Components.IndicatorCanvas)
            Components.CanvasRectTransform = Components.IndicatorCanvas.GetComponent<RectTransform>();

        CheckOptions.DefaultScale = transform.localScale;
    }

    protected virtual void FixedUpdate()
    {
        if (!Components.IndicatorAnchor)
            return;

        RefreshIndicator(
            Components.IndicatorAnchor.position,
            CheckOptions.IndicatorOffset,
            Components.RectTransform);

        ResizeIndicator();
    }


    protected virtual void RefreshIndicator(Vector3 worldPos, Vector3 worldOffset, RectTransform uiTransform)
    {
        if (!Components.MainCamera || !uiTransform || Components.CanvasRectTransform == null)
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
            Components.CanvasRectTransform,
            screenPoint,
            uiCamera,
            out Vector2 localPoint))
        {
            uiTransform.anchoredPosition = localPoint;
        }
    }

    protected void ResizeIndicator()
    {
        if (!Components.ResizeTarget 
        || !States.UseDistanceResize
        || !Components.RectTransform) return;

        //Vector3 camPos = Components.MainCamera.transform.position;

        float distance = Vector3.Distance(transform.position, Components.ResizeTarget.position);

        // distance를 MinDistance ~ MaxDistance 범위 기준으로 0~1 값으로 변환
        // distance가 MinDistance보다 작거나 같으면 0,
        // distance가 MaxDistance보다 크거나 같으면 1

        // InverseLerp : 값을 비율로 바꿈
        float t = Mathf.InverseLerp(CheckOptions.MinDistance, CheckOptions.MaxDistance, distance);
        
        // 뒤집기 작업
        t = 1f - t;

        // Lerp : 비율을 값으로 바꿈
        float scale = Mathf.Lerp(CheckOptions.MinScale, CheckOptions.MaxScale, t);

        Components.RectTransform.localScale = CheckOptions.DefaultScale * scale;
    }

    public void SetGuardZone(GuardZone guardZone)
    {
        States.IsActive = (guardZone == CheckOptions.GuardZone);
    }
    public void SetActionState(GuardIndicatorInfo.IndicatorActionState actionState)
    {
        States.ActionState = actionState;
    }

    public void SetHealthState(GuardIndicatorInfo.IndicatorHealthState healthState)
    {
        States.HealthState = healthState;
    }

    public void SetDefaultScale(Vector3 scale)
    {
        CheckOptions.DefaultScale = scale;
    }
    public void SetResizeScale(bool isResizing)
    {
        States.UseDistanceResize = isResizing;
    }

    public GuardZone GetGuardZone()
    {
        return CheckOptions.GuardZone;
    }

    public GuardIndicatorInfo.IndicatorActionState GetActionState()
    {
        return States.ActionState;
    }

    public GuardIndicatorInfo.IndicatorHealthState GetHealthState()
    {
        return States.HealthState;
    }
}
