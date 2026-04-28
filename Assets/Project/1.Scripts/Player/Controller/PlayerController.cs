using Player;
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
using static UnityEngine.EventSystems.EventTrigger;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStatusSystem))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Player.Components m_playerComponents = new();
    [SerializeField] private Player.Inputs m_playerInputs = new();
    [SerializeField] private Player.CheckOption m_checkOption = new();
    [SerializeField] private Player.CurrentState m_currentState = new();
    [SerializeField] private Player.CurrentValue m_currentValue = new();
    [SerializeField] private Player.Status m_status;

    private Player.Components Components => m_playerComponents;
    private Player.Inputs Inputs => m_playerInputs;
    private Player.CheckOption CheckOptions => m_checkOption;
    private Player.CurrentState States => m_currentState;
    private Player.CurrentValue Values => m_currentValue;
    public Player.Status Stats => m_status;

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
        if (!Components.PlayerGuardIndicator)
            Components.PlayerGuardIndicator = GetComponentInChildren<PlayerGuardIndicator>();

        if (!Components.IndicatorCanvas)
            Components.IndicatorCanvas = GetComponentInChildren<Canvas>();

        // 무기
        if (!Components.PlayerWeapon)
            Components.PlayerWeapon = Components.PlayerWeapon = GetComponentInChildren<Weapon>();

        if (!Components.PlayerWeapon) Debug.LogError("[PlayerController] Weapon 할당이 되지 않았습니다.");

        // 리지드바디 필수요소 조정.
        if (!Components.Rigidbody.isKinematic)
            Components.Rigidbody.isKinematic = true;

    }

    private void Start()
    {
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
                    // 콤보 입력 가능 구간에는 다음 공격 입력만 예약해둡니다.
                    else if (States.IsAttacking && States.CanNextAttack)
                    {
                        States.HasNextAttackInput = true;
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
                Values.FocusTarget = null;
                Values.PreviousFocusTarget = null;
                Values.FocusTargetPoint = Vector3.zero;
                States.isEnemyDetected = false;
            }

            // 포커싱 타겟과의 거리 계산.
            if (Values.FocusTarget)
                Values.FocusTargetDistance = Vector3.Distance(
                Values.FocusTarget.transform.position,
                transform.position);
        }

    }

    private void GuardIndicatorUpdate()
    {
        if (Components.IndicatorCanvas && Components.PlayerGuardIndicator)
        {
            bool showIndicator = States.IsFocusing;
            if (Components.IndicatorCanvas.enabled != showIndicator)
                Components.IndicatorCanvas.enabled = showIndicator;
        }
        else Debug.LogError("[PlayerController] IndicatorCanvas 필요.");
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
            Components.Animator.SetFloat("GuardZone", (int)Values.GuardZone);
        }
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
        List<Collider> visibleEnemies = GetVisibleFocusEnemySnapshot();
        if (visibleEnemies.Count <= 0)
        {
            States.isEnemyDetected = false;
            Values.FocusTarget = null;
            return;
        }

        //Values.DetectEnemysBuffer = Components.FocusAim.GetEnemyColliderSnapShot();
        // Aim 기준으로 2차 기준 분리
        List<Collider> aimEnemies = Components.FocusAim.GetEnemyColliderSnapShot();

        List<Collider> insideAimEnemies = new List<Collider>();    // 1순위
        List<Collider> outsideAimEnemies = new List<Collider>();   // 2순위
        
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Collider enemy = visibleEnemies[i];
            if (enemy == null)
                continue;

            if (aimEnemies.Contains(enemy))
                insideAimEnemies.Add(enemy);
            else
                outsideAimEnemies.Add(enemy);
        }

        // 마지막으로 화면 중앙과 거리 비교로 필터링 마침
        Collider finalTarget = FindAimClosetTarget(insideAimEnemies);

        if (finalTarget == null)
            finalTarget = FindAimClosetTarget(outsideAimEnemies);

        Values.FocusTarget = finalTarget;
        States.isEnemyDetected = finalTarget != null;

    }

    private List<Collider> GetVisibleFocusEnemySnapshot()
    {
        List<Collider> visibleEnemies = new List<Collider>();

        if (Components.MainCamera == null || Components.FocusAim == null)
            return visibleEnemies;

        Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);

        int detectEnemyCount =
            Physics.OverlapBoxNonAlloc(
            Components.FocusAim.transform.TransformPoint(CheckOptions.OverlapBoxForwardOffset),
            CheckOptions.OverlapBoxHalfExtents,
            Values.DetectEnemysBuffer,
            Quaternion.Euler(0f, Components.FocusAim.transform.eulerAngles.y, 0f),
            Global.EnemyLayerMask,
            QueryTriggerInteraction.Ignore);

        if (detectEnemyCount <= 0)
            return visibleEnemies;

        foreach (Collider enemy in Values.DetectEnemysBuffer)
        {
            if (enemy == null)
                continue;

            Vector3 viewPos = Components.MainCamera.WorldToViewportPoint(enemy.bounds.center);

            if (viewPos.z <= 0f)
                continue;

            if (viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f)
                continue;

            visibleEnemies.Add(enemy);
        }

        return visibleEnemies;
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
    // 이동
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

    /// <summary>
    /// 기본 상태
    /// </summary>
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

    /// <summary>
    /// 집중 상태
    /// </summary>
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

            // 카메라의 정면/오른쪽 벡터를 바닥 평면(XZ)에 투영해서
            // no-target 포커스 상태에서도 화면 기준 좌우 이동 조작을 유지.
            Vector3 camForward = Vector3.ProjectOnPlane(Components.MainCamera.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(Components.MainCamera.transform.right, Vector3.up).normalized;

            // 타겟이 없을 때는 몸 방향도 카메라 정면으로 고정.
            if (camForward.sqrMagnitude > 0.0001f)
                Components.Rigidbody.MoveRotation(Quaternion.LookRotation(camForward));

            if (!States.IsMoving)
            {
                Values.PlayerVelocity = Vector3.zero;
                return;
            }

            Vector3 curMoveDir = camRight * moveInput.x + camForward * moveInput.y;

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
            // 실제 타겟 위치는 바로 바뀔 수 있으므로, 바라보는 기준점은
            // FocusTargetPoint를 통해 한 번 부드럽게 보간해서 씁니다.
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
    private void Parry()
    {
        if (!States.IsAttacking) return;

        SetIsParrying(true);

        if (States.IsFocusing)
            ParryFocus();
        else
            ParryDefault();
    }

    /// <summary>
    /// 기본 상태
    /// </summary>
    private void ParryDefault()
    {
        Components.Animator.SetTrigger("DoParry");
    }
    /// <summary>
    /// 집중 상태
    /// </summary>
    private void ParryFocus()
    {
        Components.Animator.SetTrigger("DoParry");
    }

    public void SetIsParrying(bool isParrying)
    {
        States.IsParrying = isParrying;

        if (Components.Animator)
            Components.Animator.SetBool("IsParrying", isParrying);
    }
    #endregion


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 쳐내기(패리)
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 쳐내기(패리) 로직 구간입니다.
    /// </summary>
    #region Attack Methods
    private void Attack()
    {

        if (States.IsFocusing)
            AttackFocus();
        else
            AttackDefault();
    }

    /// <summary>
    /// 기본 상태
    /// </summary>
    private void AttackDefault()
    {
        SetIsAttacking(true);
        Components.Animator.SetTrigger("DoAttack");
    }
    /// <summary>
    /// 집중 상태
    /// </summary>
    private void AttackFocus()
    {
        if (!States.IsAttacking) return;
        SetIsAttacking(true);
        Components.Animator.SetTrigger("DoAttack");
    }

    public void SetIsAttacking(bool isAttacking)
    {
        States.IsAttacking = isAttacking;

        if (!isAttacking)
        {
            States.CanNextAttack = false;
            States.HasNextAttackInput = false;
        }

        if (Components.Animator)
            Components.Animator.SetBool("IsAttacking", isAttacking);
    }

    public void SetCanNextAttack(bool canNextAttack)
    {
        States.CanNextAttack = canNextAttack;
    }

    public bool GetHasNextAttackInput()
    {
        return States.HasNextAttackInput;
    }

    public void SetHasNextAttackInput(bool hasNextAttackInput)
    {
        States.HasNextAttackInput = hasNextAttackInput;
    }

    public bool GetIsFocusing()
    {
        return States.IsFocusing;
    }

    public bool GetIsAttacking()
    {
        return States.IsAttacking;
    }

    public bool GetIsParrying()
    {
        return States.IsParrying;
    }

    public Collider GetFocusTarget()
    {
        return Values.FocusTarget;
    }
    #endregion



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
        Color radiusColor = States.isEnemyDetected ? Color.orangeRed : Color.olive;
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
