using System;
using GuardIndicatorInfo;
using Game.Combat.Execution;
using UnityEngine;
using Unity.VectorGraphics;


public class EnemyGuardIndicator : GuardIndicatorBase
{
    [SerializeField] private EnemyCombatController m_enemyController;
    

    protected override void Awake()
    {
        base.Awake();

        if (!m_enemyController)
            m_enemyController = GetComponentInParent<EnemyCombatController>();

        if (!m_enemyController)
            Debug.LogError("[EnemyGuardIndicator] EnemyCombatController 할당이 되지 않았습니다.");
    }

    protected void Start()
    {
        if (!Components.ResizeTarget)
            Components.ResizeTarget = PlayerManager.Instance.GetPlayerTransform();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    protected override void RefreshIndicator(Vector3 worldPos, Vector3 worldOffset, RectTransform uiTransform)
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
}

