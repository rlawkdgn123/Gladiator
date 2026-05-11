using PlayerControllerInfo;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static UnityEditor.VersionControl.Asset;
using static UnityEngine.EventSystems.EventTrigger;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStatusSystem))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerControllerInfo.Components m_components = new();
    [SerializeField] private PlayerControllerInfo.Inputs m_inputs = new();
    [SerializeField] private PlayerControllerInfo.CheckOption m_checkOption = new();
    [SerializeField] private PlayerControllerInfo.CurrentState m_currentState = new();
    [SerializeField] private PlayerControllerInfo.CurrentValue m_currentValue = new();
    [SerializeField] private PlayerControllerInfo.Status m_status;

    private PlayerControllerInfo.Components Components => m_components;
    private PlayerControllerInfo.Inputs Inputs => m_inputs;
    private PlayerControllerInfo.CheckOption CheckOptions => m_checkOption;
    private PlayerControllerInfo.CurrentState States => m_currentState;
    private PlayerControllerInfo.CurrentValue Values => m_currentValue;
    public PlayerControllerInfo.Status Stats => m_status;

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 초기화
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 초기화 구간입니다.
    /// </summary>
    #region Awake&Start Methods
    private void Awake()
    {
        //////////////////////////////////////////////////////////////////////
        // 플래이어 내부 컴포넌트 할당.
        //////////////////////////////////////////////////////////////////////
        
        if (!Components.PlayerStatus)
            Components.PlayerStatus = GetComponent<PlayerStatusSystem>();
        if (Components.PlayerStatus)
            m_status = Components.PlayerStatus.Stats;

        if (!Components.PlayerStatus)
            Debug.LogError("[PlayerController] PlayerStatus 할당이 되지 않았습니다.");

        if (!Components.Animator)
            Components.Animator = GetComponent<Animator>();

        if (!Components.Rigidbody)
            Components.Rigidbody = GetComponent<Rigidbody>();

        if (!Components.CapsuleCollider)
            Components.CapsuleCollider = GetComponent<CapsuleCollider>();

        if (!Components.PlayerInput)
            Components.PlayerInput = GetComponent<PlayerInput>();

        // 인풋 핸들러
        if (!Components.InputHandler)
            Components.InputHandler = GetComponent<PlayerInputHandler>();

        // 가드 인디케이터
        if (!Components.PlayerGuardIndicatorSystem)
            Components.PlayerGuardIndicatorSystem = GetComponentInChildren<PlayerGuardIndicatorSystem>();

        if (!Components.IndicatorCanvas)
            Components.IndicatorCanvas = GetComponentInChildren<Canvas>();

        // 무기
        if (!Components.PlayerWeapon)
            Components.PlayerWeapon = Components.PlayerWeapon = GetComponentInChildren<Weapon>();

        if (!Components.PlayerWeapon) Debug.LogError("[PlayerController] Weapon 할당이 되지 않았습니다.");

        // 리지드바디 필수요소 조정.
        if (!Components.Rigidbody.isKinematic)
            Components.Rigidbody.isKinematic = true;

        //////////////////////////////////////////////////////////////////////
        // 플래이어 외부 컴포넌트 할당.
        //////////////////////////////////////////////////////////////////////
        // 커서 매니저
        if (!Components.CursorManager)
            Components.CursorManager = CursorManager.Instance;

        if (!Components.CursorManager) Debug.LogError("[PlayerController] CursorManager 할당이 되지 않았습니다.");

        // 포커스 모드 에임
        if (!Components.FocusAim)
            Components.FocusAim = Components.FocusAim = GameObject.Find("PlayerTargetManager").GetComponent<PlayerTargetFinder>();

        if (!Components.FocusAim) Debug.LogError("[PlayerController] PlayerFocusAim 할당이 되지 않았습니다.");

        // 카메라 할당.
        if (!Components.MainCamera)
            Components.MainCamera = Camera.main;

    }

    private void Start()
    {

        EnsureDetectEnemyBuffer();
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 업데이트
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 업데이트 구간입니다.
    /// </summary>
    #region Update Methods
    private void FixedUpdate()
    {
        Move();               // 플레이어 이동 분기.
    }

    private void Update()
    {
        StateUpdate();          // 플레이어 상태 업데이트. [Frame]
        UpdateFocusTargetTransition(); // 포커스 타겟 전환 보간 및 정지 중 회전 처리.
        UpdateCursorState();    // 디폴트 모드 커서 상태 처리.
        GuardIndicatorUpdate(); // 가드 인디케이터 상태 처리.
        DrawFocusDebug();       // 포커스 디버그 표시.
    }

    private void LateUpdate()
    {
        
    }
    #endregion
    ////////////////////////////////////////////////////////////////////////////////////////////////////


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 상태 갱신
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 상태 갱신 구간입니다.
    /// 애니메이션 관리 기능도 포함됩니다.
    /// </summary>
    #region State&AnimatorState Methods
    private void StateUpdate()
    {
        Camera mainCam = Components.MainCamera;

        // 인풋 상태 처리.
        if (Components.InputHandler)
        {
           Vector2 moveInput = Inputs.MoveVector = Components.InputHandler.GetMoveVector(); // 이동 벡터
            Vector2 lookInput = Inputs.LookVector = Components.InputHandler.GetLookVector(); // 화면 벡터
            float scrollInput = Inputs.ScrollY = Components.InputHandler.GetScrollY(); // 화면 벡터
            bool isSprintPressed = Components.InputHandler.GetSprintActionIsPressed();

            Inputs.ScreenCenter = new Vector3(mainCam.pixelWidth / 2, mainCam.pixelHeight / 2);
            Inputs.Aim = mainCam.ScreenPointToRay(Inputs.ScreenCenter); Debug.DrawRay(transform.position, transform.forward * 10f, Color.red);
            
            

            // 방향 및 공격.
            {
                if (Components.InputHandler.GetAttackActionWasPressedThisFrame())
                {
                    // 기본 공격 시작.
                    if (!States.IsAttacking && !States.IsPerformingAction)
                    {
                        States.IsAttacking = true;
                        Attack();
                    }
                    // 콤보 입력 가능 구간에는 다음 공격 트리거를 바로 다시 보냅니다.
                    else if (States.IsAttacking && States.CanNextAttack)
                    {
                        Components.Animator.SetTrigger("DoAttack");
                    }
                }
            }


            // 움직이기. (Default : Run / Focus : Walk)
            {
                States.IsMoving = moveInput.sqrMagnitude >= 0.0001f;
                Components.Animator.SetBool("IsMoving", States.IsMoving);


                // 1. 이번 프레임에 얼마나 따라갈지 비율 t를 만든다
                // 2. 그 t로 현재값을 목표값 쪽으로 당긴다
                // CheckOptions.MoveAmountLerpSpeed : 반응속도.
                // Mathf.Exp : 지수함수.
                //MoveAmountLerpSpeed가 클수록 한 프레임에 목표값쪽으로 더 빨리 따라감
                float moveAmountLerpT = 1f - Mathf.Exp(-CheckOptions.MoveAmountLerpSpeed * Time.deltaTime);
                Values.MoveAmount = Mathf.Lerp(
                    Values.MoveAmount,
                    Values.TargetMoveAmount,
                    moveAmountLerpT);

                if (States.IsFocusing)
                {
                    // 포커스 모드
                    float focusMoveInputLerpT = 1f - Mathf.Exp(-CheckOptions.FocusMoveInputLerpSpeed * Time.deltaTime);
                    Values.MoveInputX = Mathf.Lerp(Values.MoveInputX, moveInput.x, focusMoveInputLerpT);
                    Values.MoveInputZ = Mathf.Lerp(Values.MoveInputZ, moveInput.y, focusMoveInputLerpT);

                    Components.Animator.SetFloat("MoveInputX", Values.MoveInputX);
                    Components.Animator.SetFloat("MoveInputZ", Values.MoveInputZ);
                }
                else
                {
                    // 디폴트 모드
                    Values.MoveInputX = 0f;
                    Values.MoveInputZ = 0f;
                    Components.Animator.SetFloat("MoveInputX", 0f);
                    Components.Animator.SetFloat("MoveInputZ", Values.MoveAmount);
                    Components.Animator.SetFloat("Blend", Values.MoveAmount);
                }
            }

            // 화면 전환.
            {
                // 애니메이션 설정.
                Components.Animator.SetFloat("LookInputX", lookInput.x);
                Components.Animator.SetFloat("LookInputY", lookInput.y);
            }

            States.IsStrafing = Mathf.Abs(moveInput.x) > 0.0001f;
            Components.Animator.SetBool("IsStrafing", States.IsStrafing);

            // 전력질주.
            {
                States.IsSprinting = isSprintPressed;
                Components.Animator.SetBool("IsSprinting", States.IsSprinting);
            }

            if (Components.InputHandler.GetFocusActionWasPressedThisFrame())
            {
                States.IsFocusing = !States.IsFocusing;

                if (States.IsFocusing && !Values.FocusTarget)
                    DetectEnemy(); // 포커스 카메라 전환 전에 실행.

                Components.Animator.SetBool("IsFocusing", States.IsFocusing);
            }

            // 초회 검사 이후 적 찾기
            if (States.IsFocusing && !Values.FocusTarget)
            {
                DetectEnemy();
            }


            if (States.IsFocusing && Values.FocusTarget && Mathf.Abs(scrollInput) > 0.0001f)
            {
                ChangeFocusTarget(scrollInput);
                Components.InputHandler.SetScrollY(0f);
                Inputs.ScrollY = 0f;
            }
                
        }
        else Debug.LogError("[PlayerController] InputHandler 필요.");

        // 인풋 조건 처리.
        {
            if (!States.IsFocusing && States.isEnemyDetected)
            {
                // 초기화.
                Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);
                Values.AimEnemysBuffer.Clear();
                Values.FocusTarget = null;
                Values.PreviousFocusTarget = null;
                Values.FocusTargetPoint = Vector3.zero;
                Values.FocusNoTargetForward = Vector3.zero;
                Values.FocusNoTargetRight = Vector3.zero;
                States.isEnemyDetected = false;
            }

            // 포커싱 타겟과의 거리 계산.
            if (Values.FocusTarget)
                Values.FocusTargetDistance = Vector3.Distance(
                Values.FocusTarget.transform.position,
                transform.position);
        }


        // 가드 가능 상태 : Trigger 행동을 하지 않고 있는 기본 상태
        States.CanGuard = (!States.IsAttacking && !States.IsParrying);
        Components.Animator.SetBool("IsGuard", States.CanGuard);

    }

    private void GuardIndicatorUpdate()
    {
        if (Components.IndicatorCanvas && Components.PlayerGuardIndicatorSystem)
        {
            bool showIndicator = States.IsFocusing;
            if (Components.IndicatorCanvas.enabled != showIndicator)
                Components.IndicatorCanvas.enabled = showIndicator;
        }
        else Debug.LogError("[PlayerController] IndicatorCanvas 필요.");
    }

    private void UpdateFocusTargetTransition()
    {
        if (!States.IsFocusing || Values.FocusTarget == null)
            return;

        if (!TryUpdateFocusTargetPoint(Time.deltaTime))
            return;

        if (States.IsMoving)
            return;

        Vector3 playerPos = Components.Rigidbody.position;
        Vector3 lookDir = Vector3.ProjectOnPlane(Values.FocusTargetPoint - playerPos, Vector3.up);

        if (lookDir.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(lookDir);
        Values.FocusTargetDistance = Vector3.Distance(Values.FocusTargetPoint, playerPos);
    }

    private void UpdateCursorState()
    {

        bool shouldLockCursor = !States.IsFocusing;
        CursorLockMode targetLockMode = shouldLockCursor
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        if (Cursor.lockState != targetLockMode)
            Cursor.lockState = targetLockMode;

        bool shouldShowCursor = !shouldLockCursor;
        if (Cursor.visible != shouldShowCursor)
            Cursor.visible = shouldShowCursor;

        if (CursorManager.Instance && !States.IsAttacking)
        {
            Values.GuardZone = CursorManager.Instance.GetGuardZoneDirection();
            Components.Animator.SetInteger("GuardZone", (int)Values.GuardZone);
        }
    }

    //////////////////////////////
    /// <summary>
    /// Getter / Setter
    /// </summary>
    //////////////////////////////
    public bool GetIsFocusing()
    {
        return States.IsFocusing;
    }

    public Collider GetFocusTarget()
    {
        return Values.FocusTarget;
    }
    #endregion


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 적 탐지
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 적 탐지 로직 구간입니다.
    /// </summary>
    #region Detect Methods
    //////////////////////////////////////////////////////////////////////
    /// DetectEnemy()
    //////////////////////////////////////////////////////////////////////
    /// [적 감지 기준]
    /// 
    /// 1. 마우스 휠 클릭으로 포커싱 (에임두고)
    /// 2. 포커싱된 애 근처에 뭐가 있다면, 마우스 휠로 포커싱 전환 가능
    /// 3. 포커싱된 애가 죽었을 때, 근처에 다른 적이 있으면 리타겟팅, 없으면 디폴트모드로 전환
    /// 
    /// 리타겟팅 우선순위
    /// 
    /// 1. 첫 포커싱을 할 때는, 에임 중앙에서 거리순
    /// 2. 이미 포커싱되었을 때, 마우스 휠로 바뀌는 기준
    /// 
    /// → A, B, C중 B를 타겟팅하고 있다고 가정,
    //////////////////////////////    
    ///  A B C        // Enemys //
    ///   <P>         // Player //
    //////////////////////////////
    /// 휠업 → 왼쪽인 A 리타겟팅 (반시계방향)
    /// 휠다운 → 오른쪽인 C 리타겟팅 (시계방향)
    /// 
    /// 입력 한 번에 검사 한 번
    //////////////////////////////////////////////////////////////////////
    /// </summary>
    private void DetectEnemy()
    {
        EnsureDetectEnemyBuffer();

        List<Collider> visibleEnemies = GetVisibleFocusEnemySnapshot();
        if (visibleEnemies.Count <= 0)
        {
            Debug.Log("[FocusDetect] visibleEnemies.Count <= 0");
            States.isEnemyDetected = false;
            Values.FocusTarget = null;
            return;
        }

        // Aim 기준으로 2차 기준 분리
        Values.AimEnemysBuffer = Components.FocusAim.GetEnemyColliderSnapShot();

        List<Collider> insideAimEnemies = new List<Collider>();    // 1순위
        List<Collider> outsideAimEnemies = new List<Collider>();   // 2순위
        
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Collider enemy = visibleEnemies[i];
            if (enemy == null)
                continue;

            if (Values.AimEnemysBuffer.Contains(enemy))
                insideAimEnemies.Add(enemy);
            else
                outsideAimEnemies.Add(enemy);
        }

        // 마지막으로 화면 중앙과 거리 비교로 필터링 마침
        Collider finalTarget = FindAimClosetTarget(insideAimEnemies);

        if (finalTarget == null)
            finalTarget = FindAimClosetTarget(outsideAimEnemies);

        Debug.Log(
            $"[FocusDetect] visible:{visibleEnemies.Count}, insideAim:{insideAimEnemies.Count}, outsideAim:{outsideAimEnemies.Count}, finalTarget:{(finalTarget ? finalTarget.name : "null")}");

        Values.FocusTarget = finalTarget;
        States.isEnemyDetected = finalTarget != null;

    }


    private List<Collider> GetVisibleFocusEnemySnapshot()
    {
        Debug.Log("[FocusDetect] GetVisibleFocusEnemySnapshot()");
        List<Collider> visibleEnemies = new List<Collider>();

        if (Components.MainCamera == null || Components.FocusAim == null)
        {
            Debug.LogWarning(
                $"[FocusDetect] MainCamera 또는 FocusAim 없음. MainCamera:{(Components.MainCamera ? Components.MainCamera.name : "null")}, FocusAim:{(Components.FocusAim ? Components.FocusAim.name : "null")}");
            return visibleEnemies;
        }

        EnsureDetectEnemyBuffer();
        Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);

        Quaternion overlapRotation =
        Quaternion.Euler(0f, Components.FocusAim.transform.eulerAngles.y, 0f);

        Vector3 overlapCenter =
            Components.FocusAim.transform.position + (overlapRotation * CheckOptions.OverlapBoxForwardOffset);

        int overlapLayerMask =
            CheckOptions.DebugDetectAllLayers
            ? Physics.AllLayers
            : Global.EnemyLayerMask;

        int detectEnemyCount =
            Physics.OverlapBoxNonAlloc(
            overlapCenter,
            CheckOptions.OverlapBoxHalfExtents,
            Values.DetectEnemysBuffer,
            overlapRotation,
            overlapLayerMask,
            QueryTriggerInteraction.Ignore);

        Debug.Log(
            $"[FocusDetect] OverlapBox center:{overlapCenter}, halfExtents:{CheckOptions.OverlapBoxHalfExtents}, detectEnemyCount:{detectEnemyCount}, bufferSize:{Values.DetectEnemysBuffer.Length}, detectAllLayers:{CheckOptions.DebugDetectAllLayers}, layerMask:{overlapLayerMask}");

        if (detectEnemyCount <= 0)
            return visibleEnemies;

        foreach (Collider enemy in Values.DetectEnemysBuffer)
        {
            if (enemy == null)
            {
                Debug.Log("[FocusDetect] buffer 안에 null enemy");
                continue;
            }

            Debug.Log(
                $"[FocusDetect] raw hit - name:{enemy.name}, layer:{LayerMask.LayerToName(enemy.gameObject.layer)}, tag:{enemy.tag}, isTrigger:{enemy.isTrigger}");

            Vector3 viewPos = Components.MainCamera.WorldToViewportPoint(enemy.bounds.center);

            if (viewPos.z <= 0f)
            {
                Debug.Log($"[FocusDetect] {enemy.name} 탈락 - 카메라 뒤. viewport:{viewPos}");
                continue;
            }

            if (viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f)
            {
                Debug.Log(
                    $"[FocusDetect] {enemy.name} 탈락 - viewport 밖. viewport:{viewPos}, center:{enemy.bounds.center}");
                continue;
            }

            Debug.Log(
                $"[FocusDetect] {enemy.name} 통과 - viewport:{viewPos}, center:{enemy.bounds.center}");
            visibleEnemies.Add(enemy);
        }

        Debug.Log($"[FocusDetect] visibleEnemies 최종:{visibleEnemies.Count}");
        return visibleEnemies;
    }

    private void EnsureDetectEnemyBuffer()
    {
        if (Values.DetectEnemysBuffer == null || Values.DetectEnemysBuffer.Length <= 0)
        {
            int bufferSize = Mathf.Max(Global.MaxPlayersPerTeam, 8);
            Values.DetectEnemysBuffer = new Collider[bufferSize];
            Debug.LogWarning($"[FocusDetect] DetectEnemysBuffer가 비어 있어 크기 {bufferSize}로 재초기화했습니다.");
        }
    }

    private void ChangeFocusTarget(float scrollY)
    {
        if (Components.MainCamera == null || !Values.FocusTarget)
            return;

        List<Collider> visibleEnemies = GetVisibleFocusEnemySnapshot();
        if (visibleEnemies.Count <= 0)
            return;

        Collider nextTarget = FindScrollFocusTarget(visibleEnemies, scrollY);
        if (nextTarget == null)
            return;

        Values.FocusTarget = nextTarget;
        States.isEnemyDetected = true;
    }

    private Collider FindScrollFocusTarget(List<Collider> targetColliders, float scrollY)
    {
        if (targetColliders == null || targetColliders.Count == 0 || !Values.FocusTarget)
            return null;

        Vector3 currentViewPos = Components.MainCamera.WorldToViewportPoint(Values.FocusTarget.bounds.center);
        if (currentViewPos.z <= 0f)
            return null;

        bool isWheelUp = scrollY > 0f;
        float bestScore = float.MaxValue;
        Collider bestTarget = null;

        foreach (Collider target in targetColliders)
        {
            if (!target || target == Values.FocusTarget)
                continue;

            Vector3 targetViewPos = Components.MainCamera.WorldToViewportPoint(target.bounds.center);
            if (targetViewPos.z <= 0f)
                continue;

            float deltaX = targetViewPos.x - currentViewPos.x;

            if (isWheelUp)
            {
                if (deltaX >= -0.0001f)
                    continue;
            }
            else
            {
                if (deltaX <= 0.0001f)
                    continue;
            }

            float deltaY = Mathf.Abs(targetViewPos.y - currentViewPos.y);
            float score = Mathf.Abs(deltaX) + deltaY * 0.35f;

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = target;
        }

        return bestTarget;
    }


    Collider FindAimClosetTarget(List<Collider> targetColliders)
    {
        if (targetColliders == null || targetColliders.Count == 0)
            return null;


        // 화면 중앙과 가장 가까운 적 선택.
        float closestViewportDistanceSqr = float.MaxValue;
        Collider closestEnemy = null;
        Vector2 viewportCenter = Pivot.Center;

        // 화면 중앙 기준 최소 거리 콜라이더 구하기
        foreach (Collider detectedEnemy in targetColliders)
        {
            if (!detectedEnemy)
                continue;

            Vector3 viewPos = Components.MainCamera.WorldToViewportPoint(detectedEnemy.bounds.center);

            if (viewPos.z <= 0f)
                continue;

            if (viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f)
                continue;

            Vector2 enemyViewportPos = new Vector2(viewPos.x, viewPos.y);
            float distanceToCenter = Vector2.SqrMagnitude(enemyViewportPos - viewportCenter);// 길이 비교용벡터

            if (distanceToCenter >= closestViewportDistanceSqr)
                continue;

            closestViewportDistanceSqr = distanceToCenter;
            closestEnemy = detectedEnemy;
        }

        return closestEnemy;
    }

    #endregion


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 이동 및 회전
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 이동 로직 구간입니다.
    /// 모든 게임오브젝트의 이동 및 회전, 물리 연산은 FixedUpdate()에서 이뤄집니다.
    /// </summary>
    #region Move&Rotate Methods
    private void Move()
    {
        Vector2 moveInput = Inputs.MoveVector;
        Values.MoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Values.ForwardVector = Components.Rigidbody.rotation * Vector3.forward; // 플레이어 현재 바라보는 방향
        // Values.ForwardVector = transform.forward; // 플레이어 현재 바라보는 방향

        if (!States.IsMoving || States.IsAttacking)
        {
            Values.PlayerVelocity = Vector3.zero;
            Values.TargetMoveAmount = 0f;
            Values.DefaultMoveDirection = Vector3.zero;
            return;
        }

        if (States.IsFocusing)
        {
            Values.TargetMoveAmount = 0f;
            Values.DefaultMoveDirection = Vector3.zero;
            MoveFocus();
        }
        else
            MoveDefault();
    }

    //////////////////////////////
    /// <summary>
    /// 기본 상태
    /// </summary>
    //////////////////////////////
    private void MoveDefault()
    {
        if (Components.MainCamera == null)
            return;

        // 이동속도.
        float moveSpeed = States.IsSprinting
            ? Stats.SprintSpeed
            : Stats.RunningSpeed;

        Vector3 camForward = Components.MainCamera.transform.forward.normalized;
        Vector3 camRight = Components.MainCamera.transform.right.normalized;
        //Vector3 camForward = m_cachedCamForward;
        //Vector3 camRight = m_cachedCamRight;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
        {
            Values.PlayerVelocity = Vector3.zero;
            return;
        }

        Vector3 playerForward = Values.ForwardVector.normalized;
        playerForward.y = 0f;
        playerForward.Normalize();

        // 현재 사용될 값 (카메라 기준 XZ 방향 적용)
        Vector3 curMoveDir = 
            (camRight * Values.MoveDirection.x + 
            camForward * Values.MoveDirection.z)
            .normalized;

        // 회전 단계
        Quaternion targetRotation = Quaternion.LookRotation(curMoveDir);
        Quaternion nextRotation =
            Quaternion.RotateTowards(
            Components.Rigidbody.rotation,
            targetRotation,
            Stats.RotateSpeed * Time.fixedDeltaTime);

        Components.Rigidbody.MoveRotation(nextRotation);
        // 이동 단계

        // 플레이어 벡터와 카메라 벡터의 차이 각도 구하기
        float angle = playerForward.sqrMagnitude < 0.0001f
            ? 0f
            : Vector3.Angle(playerForward, curMoveDir);

        float currentSpeed;
        // 회전 가속 버전 1 (미사용)
        /*
        {
            // 현재 이동속도 회전에 따라 구분하기
            // 차이 각도가 리커버앵글보다 작으면 가속 (리커버앵글 도달 시 원래 이속)
             float currentSpeed = moveSpeed;
           if (angle > CheckOptions.MoveSpeedRecoverAngle )
            {
                // 현재 이동속도까지 보간

                // 선형 보간
                //float t = 1f - Mathf.Clamp01(angle / CheckOptions.MoveSpeedRecoverAngle);
                //currentSpeed = Mathf.Lerp(CheckOptions.TurningMoveSpeed, moveSpeed, t);

                // 비선형 보간 : MoveSpeedRecoverPower로 보간값 조절
                // MoveSpeedRecoverPower를 1로 하면 선형 보간
                float t = 1f - Mathf.Clamp01(angle / CheckOptions.MoveSpeedRecoverAngle);
                float curvedT = 1f - Mathf.Pow(1f - t, CheckOptions.MoveSpeedRecoverPower);

                currentSpeed = Mathf.Lerp(
                    CheckOptions.TurningMoveSpeed,
                    moveSpeed,
                    curvedT);
            }
        }
        */

        // 회전 가속 버전 2
        /*
        {
            
            // 리커버앵글만 없애고 연속 보간

            // 각도 차이를 기반으로 TurningMoveSpeed ~moveSpeed 사이를 연속 보간
            // MoveSpeedRecoverPower : 회복 곡선의 모양을 바꿔서 속도가 언제부터 강하게 회복될지 조절하는 값

            float angle01 = Mathf.Clamp01(angle / 180f);
            float t = 1f - angle01;
            float curvedT = 1f - Mathf.Pow(1f - t, CheckOptions.MoveSpeedRecoverPower);

            currentSpeed = Mathf.Lerp(
                CheckOptions.TurningMoveSpeed,
                moveSpeed,
                curvedT);
        }
        */

        // 회전 가속 버전 1-수정
        {
            // CheckOptions.MoveMaxRecoverAngle
            // 이 각도 이내에서는 이미 최대 속도(moveSpeed)에 도달한 것으로 간주하는 허용 구간
            // 해당 각도 이내 도달 시 최대속도로 카메라와 동일해질 때까지 속도 유지
            // MoveMaxRecoverAngle을 0도로 할 시 회전 가속 버전 2와 동일
            currentSpeed = moveSpeed;

            if (angle > CheckOptions.FullSpeedStartAngle)
            {
                float remainAngle = 180f - CheckOptions.FullSpeedStartAngle;
                float overAngle = angle - CheckOptions.FullSpeedStartAngle;

                float t = 1f - Mathf.Clamp01(overAngle / remainAngle);
                float curvedT = 1f - Mathf.Pow(1f - t, CheckOptions.MoveSpeedRecoverPower);

                currentSpeed = Mathf.Lerp(
                    Stats.TurningMoveSpeed,
                    moveSpeed,
                    curvedT);
            }
        }

        if (States.IsSprinting)
        {
            Values.TargetMoveAmount = Mathf.Clamp01(
                currentSpeed / Mathf.Max(Stats.SprintSpeed, 0.0001f));
        }
        else
        {
            Values.TargetMoveAmount =
                Mathf.Clamp01(currentSpeed / Mathf.Max(Stats.RunningSpeed, 0.0001f)) * 0.3f;
        }

            Vector3 nextPosition =
                Components.Rigidbody.position + curMoveDir * (currentSpeed * Time.fixedDeltaTime);
        Components.Rigidbody.MovePosition(nextPosition);
        Values.PlayerVelocity = curMoveDir * currentSpeed;
    }

    //////////////////////////////
    /// <summary>
    /// 집중 상태
    /// </summary>
    //////////////////////////////
    private void MoveFocus()
    {
        if (Components.MainCamera == null)
            return;

        Vector2 moveInput = Inputs.MoveVector;
        Vector3 playerPos = Components.Rigidbody.position;
        float dt = Time.fixedDeltaTime;

        // 포커스는 두 갈래로 나뉩니다.
        // 1. 타겟이 아직 없으면 카메라 정면을 기준으로 제한 이동만 처리
        // 2. 타겟이 있으면 타겟을 중심으로 공전/접근/이탈을 계산
        if (!Values.FocusTarget)
        {
            Values.PreviousFocusTarget = null;
            Values.FocusTargetPoint = Vector3.zero;

            if (Values.FocusNoTargetForward == Vector3.zero || Values.FocusNoTargetRight == Vector3.zero)
            {
                Values.FocusNoTargetForward =
                    Vector3.ProjectOnPlane(Components.MainCamera.transform.forward, Vector3.up).normalized;
                Values.FocusNoTargetRight =
                    Vector3.ProjectOnPlane(Components.MainCamera.transform.right, Vector3.up).normalized;
            }

            Vector3 camForward = Values.FocusNoTargetForward;
            Vector3 camRight = Values.FocusNoTargetRight;

            // 타겟이 없을 때는 몸 방향도 카메라 정면으로 고정.
            if (camForward.sqrMagnitude > 0.0001f)
                Components.Rigidbody.MoveRotation(Quaternion.LookRotation(camForward));

            if (!States.IsMoving)
            {
                Values.PlayerVelocity = Vector3.zero;
                return;
            }

            // no-target 포커스에선 좌우 스트레이프 및 앞뒤이동만 허용.
            // 따라서 저장해둔 카메라 right 축 기준으로만 이동함.
            Vector3 curMoveDir = (camForward * moveInput.y) + (camRight * moveInput.x);

            if (curMoveDir.sqrMagnitude < 0.0001f)
            {
                Values.PlayerVelocity = Vector3.zero;
                return;
            }

            curMoveDir.Normalize();

            Values.PlayerVelocity = curMoveDir * Stats.WalkSpeed;
            Components.Rigidbody.MovePosition(playerPos + Values.PlayerVelocity * dt);
        }
        else
        {
            Values.FocusNoTargetForward = Vector3.zero;
            Values.FocusNoTargetRight = Vector3.zero;

            if (!TryUpdateFocusTargetPoint(dt))
                return;

            Vector3 smoothedTargetPos = Values.FocusTargetPoint;

            // 타겟 -> 플레이어 방향 벡터.
            // 반경(radius)을 구하고, 여기서부터 공전/접근 방향의 기준축을 만듭니다.
            Vector3 toPlayer = Vector3.ProjectOnPlane(playerPos - smoothedTargetPos, Vector3.up);
            float currentRadius = toPlayer.magnitude;

            if (currentRadius < 0.01f)
                return;

            Vector3 radialDir = toPlayer / currentRadius;

            // 타겟 주위를 도는 접선 방향.
            // moveInput.x 가 좌/우 공전을 담당합니다.
            Vector3 tangentDir = Vector3.Cross(radialDir, Vector3.up).normalized;

            // moveInput.x : 원 궤도를 따라 좌우로 도는 성분
            // moveInput.y : 타겟 쪽으로 파고들거나 바깥으로 빠지는 성분
            Vector3 moveDir = tangentDir * moveInput.x - radialDir * moveInput.y;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                Values.PlayerVelocity = Vector3.zero;

                // 입력이 없더라도 포커스 중에는 계속 타겟을 바라보게 유지합니다.
                Vector3 lookDir = Vector3.ProjectOnPlane(smoothedTargetPos - playerPos, Vector3.up);
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    Components.Rigidbody.MoveRotation(Quaternion.LookRotation(lookDir));
                }
                return;
            }

            moveDir.Normalize();
            Values.PlayerVelocity = moveDir * Stats.WalkSpeed;

            Vector3 nextPosition = playerPos + Values.PlayerVelocity * dt;
            Vector3 nextToTarget = Vector3.ProjectOnPlane(nextPosition - smoothedTargetPos, Vector3.up);
            float nextRadius = nextToTarget.magnitude;

            // 일정 거리까지 접근하면 그 안쪽으로는 더 못 들어가게 막습니다.
            // 앞으로 밀고 들어가는 입력(moveInput.y > 0)일 때만 반경을 고정하고,
            // 좌우 공전이나 뒤로 빠지는 입력은 그대로 허용합니다.
            if (moveInput.y > 0f && nextRadius < CheckOptions.FocusMinDistance)
            {
                nextPosition =
                    smoothedTargetPos
                    + radialDir * CheckOptions.FocusMinDistance;

                nextToTarget = Vector3.ProjectOnPlane(nextPosition - smoothedTargetPos, Vector3.up);
                nextRadius = nextToTarget.magnitude;
            }

            Vector3 lookDirection = Vector3.ProjectOnPlane(smoothedTargetPos - nextPosition, Vector3.up);

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            // 디버그/판정용 타겟 거리도 최종 적용 위치 기준으로 갱신합니다.
            Values.FocusTargetDistance = nextRadius;

            Components.Rigidbody.MovePosition(nextPosition);
            Components.Rigidbody.MoveRotation(Quaternion.LookRotation(lookDirection));
        }
    }

    private bool TryUpdateFocusTargetPoint(float dt)
    {
        if (Values.FocusTarget == null)
            return false;

        // 실제 타겟 위치는 바로 바뀔 수 있으므로, 바라보는 기준점은
        // FocusTargetPoint를 통해 한 번 부드럽게 보간해서 씀.
        Transform focusTarget = Values.FocusTarget.transform;
        Vector3 targetPos = focusTarget.position;

        if (Values.PreviousFocusTarget != Values.FocusTarget || Values.FocusTargetPoint == Vector3.zero)
        {
            if (Values.PreviousFocusTarget == null || Values.FocusTargetPoint == Vector3.zero)
                Values.FocusTargetPoint = targetPos;

            Values.PreviousFocusTarget = Values.FocusTarget;
        }

        float focusTargetLerpT = 1f - Mathf.Exp(-CheckOptions.FocusTargetLerpSpeed * dt);
        Values.FocusTargetPoint = Vector3.Lerp(
            Values.FocusTargetPoint,
            targetPos,
            focusTargetLerpT);

        return true;
    }
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 공격
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 공격 로직 구간입니다.
    /// </summary>
    #region Attack Methods
    private void Attack()
    {

        if (States.IsFocusing)
            AttackFocus();
        else
            AttackDefault();
    }

    //////////////////////////////
    /// <summary>
    /// 기본 상태
    /// </summary>
    //////////////////////////////
    private void AttackDefault()
    {
        SetIsAttacking(true);
        States.CanNextAttack = true;
        Components.Animator.SetTrigger("DoAttack");
    }

    //////////////////////////////
    /// <summary>
    /// 집중 상태
    /// </summary>
    //////////////////////////////
    private void AttackFocus()
    {
        if (!States.IsAttacking) return;
        SetIsAttacking(true);
        Components.Animator.SetTrigger("DoAttack");
    }

    //////////////////////////////
    /// <summary>
    /// 공격 관련 Getter / Setter
    /// </summary>
    //////////////////////////////
    public void SetIsAttacking(bool isAttacking)
    {
        States.IsAttacking = isAttacking;

        if (!isAttacking)
        {
            States.CanNextAttack = false;
        }

        if (Components.Animator)
            Components.Animator.SetBool("IsAttacking", isAttacking);
    }

    public void SetCanNextAttack(bool canNextAttack)
    {
        States.CanNextAttack = canNextAttack;
    }

    public void AnimEvent_SetWeaponCollider(int value)
    {
        if (!Components.PlayerWeapon)
            Components.PlayerWeapon = GetComponentInChildren<Weapon>();

        if (!Components.PlayerWeapon)
        {
            Debug.LogWarning("[PlayerController] AnimEvent_SetWeaponCollider 호출 실패: Weapon을 찾지 못했습니다.", this);
            return;
        }

        Components.PlayerWeapon.SetWeaponCollider(value);
    }
    public bool GetIsAttacking()
    {
        return States.IsAttacking;
    }
    #endregion


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 피격 (막기,쳐내기,맞기)
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 가드 & 쳐내기(패리) & 맞기(히트) 로직 구간입니다.
    /// </summary>

    ////////////////////////////////////////////////////////////
    /// <summary>
    /// 막기(가드)
    /// </summary>
    ////////////////////////////////////////////////////////////
    #region Guard Methods
    public void Guard()
    {
        if (!States.IsAttacking) return;

        SetIsGuarding(true);
        Components.Animator.SetTrigger("DoGuard");
    }

    //////////////////////////////
    /// <summary>
    /// Getter / Setter
    /// </summary>
    //////////////////////////////
    
    public void SetIsGuarding(bool isGuarding)
    {
        States.IsGuarding = isGuarding;

        if (Components.Animator)
            Components.Animator.SetBool("IsGuarding", isGuarding);
    }

    public bool GetIsGuarding()
    {
        return States.IsGuarding;
    }
    #endregion

    ////////////////////////////////////////////////////////////
    /// <summary>
    /// 쳐내기(패링)
    /// </summary>
    ////////////////////////////////////////////////////////////
    #region Parry Methods
    public void Parry()
    {
        if (!States.IsAttacking) return;

        SetIsParrying(true);

        /*if (States.IsFocusing)
            ParryFocus();
        else
            ParryDefault();*/
        Components.Animator.SetTrigger("DoParry");
    }

    //////////////////////////////
    /// <summary>
    /// Getter / Setter
    /// </summary>
    //////////////////////////////
    public void SetIsParrying(bool isParrying)
    {
        States.IsParrying = isParrying;

        if (Components.Animator)
            Components.Animator.SetBool("IsParrying", isParrying);
    }

    public bool GetIsParrying()
    {
        return States.IsParrying;
    }
    #endregion

    ////////////////////////////////////////////////////////////
    /// <summary>
    /// 맞기(히트)
    /// </summary>
    ////////////////////////////////////////////////////////////
    #region Hit Methods
    public void Hit()
    {
        if (!States.IsAttacking) return;

        SetIsHitting(true);
        Components.Animator.SetTrigger("DoHit");
    }

    //////////////////////////////
    /// <summary>
    /// Getter / Setter
    /// </summary>
    //////////////////////////////
    public void SetIsHitting(bool isHitting)
    {
        States.IsHitting = isHitting;

        if (Components.Animator)
            Components.Animator.SetBool("IsHitting", isHitting);
    }

    public bool GetIsHitting()
    {
        return States.IsHitting;
    }
    #endregion

    //////////////////////////////
    /// <summary>
    ///  상태
    /// </summary>
    //////////////////////////////
    public void SetIsAttackReceive(bool isAttackReceive)
    {
        States.IsAttackReceive = isAttackReceive;

        if (Components.Animator)
            Components.Animator.SetBool("IsAttackReceive", isAttackReceive);
    }
    public bool GetCanGuard()
    {
        return States.CanGuard;
    }


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 디버그
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 디버그 구간입니다.
    /// </summary>
    #region Debug Methods
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (States.IsFocusing && Components.MainCamera != null)
        {
            // 감지범위. - 포아너 버전
            /*Color radiusColor = States.isEnemyDetected ? Color.orangeRed : Color.olive;
            Handles.color = radiusColor;
            Handles.DrawWireDisc(transform.position, Vector3.up, CheckOptions.DetectRadius);*/

        // 감지범위. - 글라디아토르 버전
        Color radiusColor = States.isEnemyDetected ? Color.orangeRed : Color.green;
           Handles.color = radiusColor;

            Quaternion boxRotation =
                    Quaternion.Euler(0f, Components.FocusAim.transform.eulerAngles.y, 0f);

            Vector3 boxCenter =
                Components.FocusAim.transform.position +
                boxRotation * 
                CheckOptions.OverlapBoxForwardOffset;

            Matrix4x4 prevMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(boxCenter, boxRotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, CheckOptions.OverlapBoxHalfExtents * 2f);
            Handles.matrix = prevMatrix;

            if (Values.FocusTarget)
            {
                Handles.color = Color.indianRed;
                Handles.DrawWireDisc(Values.FocusTarget.transform.position, Vector3.up, Values.FocusTargetDistance);
            }
        }
    }
#endif
    private void DrawFocusDebug()
    {
        if (!States.IsFocusing || Components.MainCamera == null)
            return;

        Debug.DrawRay(
            Inputs.Aim.origin,
            Inputs.Aim.direction * CheckOptions.DetectAimDistance,
            Color.cyan);

        if (Values.FocusTarget == null)
            return;

        Vector3 enemyPoint = Values.FocusTarget.bounds.center;
        float projectedDistance =
            Vector3.Dot(enemyPoint - Inputs.Aim.origin, Inputs.Aim.direction);

        projectedDistance = Mathf.Clamp(projectedDistance, 0f, CheckOptions.DetectAimDistance);

        Vector3 closestPointOnAimRay =
            Inputs.Aim.origin + Inputs.Aim.direction * projectedDistance;

        Debug.DrawLine(closestPointOnAimRay, enemyPoint, Color.yellow);
    }
}

#endregion
