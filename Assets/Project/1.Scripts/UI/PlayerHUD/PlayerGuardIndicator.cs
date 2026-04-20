/*
 * 플레이어 가드 인디케이터
 * - 이미지 UI 본체에 붙임
 * - UI 플레이어 추적
 */

using GuardIndicator;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace GuardIndicator{

    public enum IndicatorHealthState : int
    {
        Default = 0,
        Weak,
        Break,
    }

    public enum IndicatorActionState : int
    {
        Default = 0,
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
        public PlayerController PlayerController;
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
    public class CurrentValue
    {
        [Header("Stats")]
        [Tooltip("가드 체력.")]
        public int ArmorHealth;

    }

    [Serializable]
    public class CurrentState
    {

        [Header("Direction")]
        [Tooltip("커서가 해당하는 존에 입성했는지")]
        public bool IsActive;

        [Space]
        [Header("Direction")]
        [Tooltip("체력에 따른 게이지 상태")]
        public GuardIndicator.IndicatorHealthState HealthState; // 체력에 따른 게이지 상태
        [Tooltip("행동에 따른 게이지 상태")]
        public GuardIndicator.IndicatorActionState ActionState; // 행동에 따른 게이지 상태

    }

    [Serializable]
    public class ImageSet
    {
        [Header("Default")]
        [Tooltip("방향 표시기 중앙 위치값.")]
        public Sprite[] IndicatorSprites = new Sprite[3];
    }
}
public class PlayerGuardIndicator : MonoBehaviour
{
        [SerializeField] private GuardIndicator.Components m_components = new();
        [SerializeField] private GuardIndicator.CheckOption m_checkOption = new();
        [SerializeField] private GuardIndicator.CurrentState m_currentState = new();
        [SerializeField] private GuardIndicator.ImageSet m_imageSet = new();

        private GuardIndicator.Components Components => m_components;
        private GuardIndicator.CheckOption CheckOption => m_checkOption;
        private GuardIndicator.CurrentState States => m_currentState;
        private GuardIndicator.ImageSet Images => m_imageSet;


        private Vector3 m_rectPosition;
        private RectTransform m_canvasRectTransform;
        private void Awake()
    {
        if (!Components.RectTransform)
            Components.RectTransform = GetComponent<RectTransform>();

        if (!Components.PlayerController)
            Components.PlayerController = GetComponentInParent<PlayerController>();


        if (!Components.IndicatorAnchor && Components.PlayerController)
        {
            Transform uiAnchor = Components.PlayerController.transform.Find("IndicatorAnchor");
                Components.IndicatorAnchor = uiAnchor != null ? uiAnchor : transform.parent;
        }

        if (!Components.IndicatorAnchor)
            Debug.LogError("[Indicator] IndicatorAnchor가 할당되지 않았습니다.", this);


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

        // 포지션 갱신
        m_rectPosition = Components.RectTransform.position;
    }

    private void FixedUpdate()
    {
        // 포지션 갱신
        m_rectPosition = Components.RectTransform.position;

        // 인디케이터 위치 업데이트
        RefreshIndicator(
            Components.IndicatorAnchor.position, 
            CheckOption.IndicatorOffset,
            Components.RectTransform
        );
    }

    private void RefreshIndicator(Vector3 worldPos, Vector3 worldOffset, RectTransform UITrans) {
        if (!Components.MainCamera || !UITrans || m_canvasRectTransform == null)
            return;

        Vector3 worldPoint =
            worldPos
            + Components.IndicatorAnchor.up * worldOffset.y
            + Components.IndicatorAnchor.right * worldOffset.x
            + Components.IndicatorAnchor.forward * worldOffset.z;
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
            UITrans.anchoredPosition = localPoint;
        }
    }

    public void SetGuardZone(CursorManager.GuardZone guardZone)
    {
        States.IsActive = (guardZone == CheckOption.GuardZone);
    }

    public CursorManager.GuardZone GetGuardZone()
    {
        return CheckOption.GuardZone;
    }

    public IndicatorActionState GetActionState()
    {
        return States.ActionState;
    }

    public IndicatorHealthState GetHealthState()
    {
        return States.HealthState;
    }
}

