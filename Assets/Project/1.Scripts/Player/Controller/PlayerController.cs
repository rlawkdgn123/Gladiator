using Player;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static UnityEngine.EventSystems.EventTrigger;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Player.Components m_playerComponents = new();
    [SerializeField] private Player.Inputs m_playerInputs = new();
    [SerializeField] private Player.CheckOption m_checkOption = new();
    [SerializeField] private Player.CurrentState m_currentState = new();
    [SerializeField] private Player.CurrentValue m_currentValue = new();

    private Player.Components Components => m_playerComponents;
    private Player.Inputs Inputs => m_playerInputs;
    private Player.CheckOption CheckOptions => m_checkOption;
    public Player.CurrentState States => m_currentState;
    public Player.CurrentValue Values => m_currentValue;

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 초기화
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 초기화 구간입니다.
    /// </summary>
    private void Awake()
    {
        //////////////////////////////////////////////////////////////////////
        // 컴포넌트 할당.
        //////////////////////////////////////////////////////////////////////

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
        if (!Components.GuardIndicator)
            Components.GuardIndicator = GetComponentInChildren<PlayerGuardIndicator>();

        if (!Components.IndicatorCanvas)
            Components.IndicatorCanvas = GetComponentInChildren<Canvas>();

        // 포커스 모드 에임
        if (!Components.FocusAim)
            Components.FocusAim = Components.FocusAim = GameObject.Find("PlayerTargetManager").GetComponent<PlayerTargetFinder>();

        if (!Components.FocusAim) Debug.LogError("[PlayerController] PlayerFocusAim 할당이 되지 않았습니다.");

        // 카메라 할당.
        if (!Components.MainCamera)
            Components.MainCamera = Camera.main;

        // 리지드바디 필수요소 조정.
        if (!Components.Rigidbody.isKinematic)
            Components.Rigidbody.isKinematic = true;

    }


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 업데이트
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 업데이트 구간입니다.
    /// </summary>
    private void FixedUpdate()
    {
        FixedStateUpdate();     // 플레이어 상태 업데이트. [Fixed]
        Moving();               // 플레이어 이동 분기.
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

    private void FixedStateUpdate() // FixedUpdate가 순서상 우선 // 물리 로직 관련.
    {

    }
    ////////////////////////////////////////////////////////////////////////////////////////////////////


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 상태 갱신
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 상태 갱신 구간입니다.
    /// </summary>
    private void StateUpdate()
    {
        Camera mainCam = Components.MainCamera;

        // 인풋 상태 처리.
        if (Components.InputHandler)
        {
           Vector2 moveInput = Inputs.MoveVector = Components.InputHandler.Values.MoveVector; // 이동 벡터
            Vector2 lookInput = Inputs.LookVector = Components.InputHandler.Values.LookVector; // 화면 벡터

            Inputs.ScreenCenter = new Vector3(mainCam.pixelWidth / 2, mainCam.pixelHeight / 2);
            Inputs.Aim = mainCam.ScreenPointToRay(Inputs.ScreenCenter); Debug.DrawRay(transform.position, transform.forward * 10f, Color.red);
            
            // 움직이기. (Default : Run / Focus : Walk)
            {
                States.IsMoving = moveInput.sqrMagnitude >= 0.0001f;
                Components.Animator.SetBool("IsMoving", States.IsMoving);

                if (States.IsFocusing)
                {
                    // 포커스 모드
                    Components.Animator.SetFloat("MoveInputX", moveInput.x);
                    Components.Animator.SetFloat("MoveInputZ", moveInput.y);
                }
                else
                {
                    // 디폴트 모드
                    float inputMag = moveInput.magnitude;
                    Components.Animator.SetFloat("MoveInputX", 0f);
                    Components.Animator.SetFloat("MoveInputZ", inputMag);
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
                States.IsSprinting = Components.InputHandler.Actions.SprintAction != null
                && Components.InputHandler.Actions.SprintAction.IsPressed();
                Components.Animator.SetBool("IsSprinting", States.IsSprinting);
            }

            if (Components.InputHandler.Actions.FocusAction != null
                && Components.InputHandler.Actions.FocusAction.WasPressedThisFrame())
            {
                States.IsFocusing = !States.IsFocusing;

                if (States.IsFocusing && !Values.FocusTarget)
                    DetectEnemy(); // 포커스 카메라 전환 전에 실행.

                Components.Animator.SetBool("IsFocusing", States.IsFocusing);
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
        if (Components.IndicatorCanvas && Components.GuardIndicator)
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

        if (CursorManager.Instance)
        {
           Values.GuardZone = CursorManager.Instance.GetGuardZone();
            // 애니메이터 할당
            Components.Animator.SetInteger("CursorZone", (int)Values.GuardZone);
        }
    }


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 적 탐지
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 적 탐지 로직 구간입니다.
    /// </summary>
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
        int detectEnemyCount = 0;
        Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);

        //////////////////////////////////////////////////
        // 기존 포아너 방식 - 플레이어 주변 원 모양 감지
        // 기획상 변경으로 폐기.
        //////////////////////////////////////////////////
        {
            /*
            detectEnemyCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            CheckOptions.DetectRadius,
            Values.DetectEnemysBuffer,
            Global.EnemyLayerMask);
             */
        }


        //////////////////////////////////////////////////
        // 글라디아토르 방식 - 에임 근처 적 감지
        //////////////////////////////////////////////////
        /*
        // 1. OverlapBox를 통한 1차 스냅샷 캡처
        // 2. 캡처된 것 중, 카메라 프러스텀 내에 있는 콜라이더만 남기기
        // 3. Aim 기준으로 화면 중앙, 바깥 콜라이더 중요도 분할
        // 4. 중요도가 가장 높은 콜라이더 컨테이너 중, 가장 화면 중앙(에임)과 가까운 것 선택
        //
        // 스냅샷은 오버랩 -> 한 번만 하기 때문에 채택
        // 중앙 실린더는 콜라이더 -> 적 감지를 못하면 계속 감지해야 해서
        // 1번의 경우, 제외하면 모든 오브젝트를 검사해야하므로, 너무 비싸서 했음.
        // 혹시 모를 장애물 뒤 적도 필터링하여 제외하려고 
        // 레이캐스트 테스트도 넣을 예정이었으나 추후에 할 예정.
        */

        {
            if (Components.MainCamera == null)
            {
                States.isEnemyDetected = false;
                Values.FocusTarget = null;
                return;
            }
           

            // 1차 캡처(1회성).
            detectEnemyCount = 
                Physics.OverlapBoxNonAlloc(
                Components.FocusAim.transform.TransformPoint(CheckOptions.OverlapBoxForwardOffset),
                CheckOptions.OverlapBoxHalfExtents,
                Values.DetectEnemysBuffer,
                Quaternion.Euler(0f, Components.FocusAim.transform.eulerAngles.y, 0f),
                Global.EnemyLayerMask,
                QueryTriggerInteraction.Ignore);


            // 검색 인원 없으면 return
            if (detectEnemyCount <= 0)
            {
                States.isEnemyDetected = false;
                Values.FocusTarget = null;
                return;
            }

            // 1차 캡처에서 프러스텀 내에 있는 콜라이더만 남기기
            List<Collider> visibleEnemies = new List<Collider>();

            foreach (Collider enemy in Values.DetectEnemysBuffer)
            {
                if (enemy == null)
                    continue;
                Vector3 viewPos = Components.MainCamera.WorldToViewportPoint(enemy.bounds.center);

                if (viewPos.z <= 0f)
                    continue;

                if (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
                    continue;

                visibleEnemies.Add(enemy);
            }

            // Aim 기준으로 2차 기준 분리
            List<Collider> aimEnemies = Components.FocusAim.GetEnemyColliderSnapShot();

            List<Collider> insideAimEnemies = new List<Collider>();    // 1순위
            List<Collider> outsideAimEnemies = new List<Collider>();   // 2순위
            
            for (int i = 0; i < visibleEnemies.Count; i++)
            {
                Collider enemy = visibleEnemies[i];
                if (enemy == null)
                    continue;

                Vector3 viewPos = Components.MainCamera.WorldToViewportPoint(enemy.bounds.center);

                if (viewPos.z <= 0f)
                    continue;

                if (viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f) // 내부 위치 검사
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 이동
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 플레이어 이동 로직 구간입니다.
    /// 모든 게임오브젝트의 이동 및 회전, 물리 연산은 FixedUpdate()에서 이뤄집니다.
    /// </summary>
    private void Moving()
    {
        Vector2 moveInput = Inputs.MoveVector;
        Values.MoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Values.MoveAmount = Mathf.Clamp01(Mathf.Abs(moveInput.x) + Mathf.Abs(moveInput.y));
        Values.ForwardVector = Components.Rigidbody.rotation * Vector3.forward; // 플레이어 현재 바라보는 방향
        // Values.ForwardVector = transform.forward; // 플레이어 현재 바라보는 방향

        if (!States.IsMoving) return;

        if (States.IsFocusing)
            MovingFocus();
        else
            MovingDefault();
    }

    private void MovingDefault()
    {
        if (Components.MainCamera == null)
            return;

        // 이동속도.
        float moveSpeed = States.IsSprinting
            ? CheckOptions.SprintSpeed
            : CheckOptions.RunningSpeed;

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
            CheckOptions.RotateSpeed * Time.fixedDeltaTime);

        Components.Rigidbody.MoveRotation(nextRotation);
        // 이동 단계

        // 플레이어 벡터와 카메라 벡터의 차이 각도 구하기
        float angle = playerForward.sqrMagnitude < 0.0001f
            ? 0f
            : Vector3.Angle(playerForward, curMoveDir);


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

            Vector3 nextPosition =
                Components.Rigidbody.position + curMoveDir * (currentSpeed * Time.fixedDeltaTime);
        Components.Rigidbody.MovePosition(nextPosition);
        Values.PlayerVelocity = curMoveDir * currentSpeed;
    }

    /// <summary>
    /// 집중 상태
    /// </summary>
    private void MovingFocus()
    {
        if (Components.MainCamera == null)
            return;

        Vector2 moveInput = Inputs.MoveVector;
        Vector3 playerPos = Components.Rigidbody.position;
        float dt = Time.fixedDeltaTime;

        if (!Values.FocusTarget)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(Components.MainCamera.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(Components.MainCamera.transform.right, Vector3.up).normalized;

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

            Values.PlayerVelocity = curMoveDir * CheckOptions.WalkSpeed;
            Components.Rigidbody.MovePosition(playerPos + Values.PlayerVelocity * dt);
        }
        else
        {
            Transform focusTarget = Values.FocusTarget.transform;
            Vector3 targetPos = focusTarget.position;

            Vector3 toPlayer = Vector3.ProjectOnPlane(playerPos - targetPos, Vector3.up);
            float currentRadius = toPlayer.magnitude;

            if (currentRadius < 0.01f)
                return;

            Vector3 radialDir = toPlayer / currentRadius;

            // +가 왼쪽
            //Vector3 tangentDir = Vector3.Cross(Vector3.up, radialDir).normalized;
            // 좌우 반전 (기본값)
            Vector3 tangentDir = Vector3.Cross(radialDir, Vector3.up).normalized;

            Vector3 moveDir = tangentDir * moveInput.x - radialDir * moveInput.y;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                Values.PlayerVelocity = Vector3.zero;

                Vector3 lookDir = Vector3.ProjectOnPlane(targetPos - playerPos, Vector3.up);
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    Components.Rigidbody.MoveRotation(Quaternion.LookRotation(lookDir));
                }

                return;
            }

            moveDir.Normalize();
            Values.PlayerVelocity = moveDir * CheckOptions.WalkSpeed;

            Vector3 nextPosition = playerPos + Values.PlayerVelocity * dt;
            Vector3 lookDirection = Vector3.ProjectOnPlane(targetPos - nextPosition, Vector3.up);

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Values.FocusTargetDistance = Vector3.Distance(
                Vector3.ProjectOnPlane(nextPosition, Vector3.up),
                Vector3.ProjectOnPlane(targetPos, Vector3.up)
            );

            Components.Rigidbody.MovePosition(nextPosition);
            Components.Rigidbody.MoveRotation(Quaternion.LookRotation(lookDirection));
        }
    }
    ////////////////////////////////////////////////////////////////////////////////////////////////////


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // 디버그
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 디버그 구간입니다.
    /// </summary>
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
