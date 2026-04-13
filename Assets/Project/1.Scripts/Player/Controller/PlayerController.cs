using Cinemachine;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    private Player.CurrentState States => m_currentState;
    private Player.CurrentValue Values => m_currentValue;

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
        if (!Components.IndicatorCanvas)
        {
            PlayerGuardIndicator indicator = GetComponentInChildren<PlayerGuardIndicator>();
            if (indicator != null)
                Components.IndicatorCanvas = indicator.gameObject;
        }

        // 포커스 모드 에임
        if (!Components.FocusAim)
        {
            PlayerTargetFinder focusAim = Components.FocusAim = GameObject.Find("PlayerTargetManager").GetComponent<PlayerTargetFinder>();
            if (focusAim != null)
                Components.FocusAim = focusAim;
        }
        if (!Components.FocusAim) Debug.LogError("[PlayerController] PlayerFocusAim 할당이 되지 않았습니다.");


        // 카메라 할당.
        if (!Components.MainCamera)
            Components.MainCamera = Camera.main;

        if (!Components.OrbitCamera)
        {
            GameObject orbitCameraObject = GameObject.Find("PlayerCamera (Orbit FreeLook)");
            if (orbitCameraObject != null)
                Components.OrbitCamera = orbitCameraObject.GetComponent<CinemachineFreeLook>();
        }
        //Components.OrbitCamera = FindFirstObjectByType<CinemachineFreeLook>().GetComponent<Camera>();
        if (!Components.OrbitCamera) Debug.LogError("[PlayerController] PlayerCamera (Orbit FreeLook) 할당이 되지 않았습니다.");

        if (!Components.OrbitInputProvider && Components.OrbitCamera)
            Components.OrbitInputProvider = Components.OrbitCamera.GetComponent<CinemachineInputProvider>();

        if (!Components.OrbitInputProvider)
            Debug.LogError("[PlayerController] OrbitCamera의 CinemachineInputProvider 할당이 되지 않았습니다.");

        if (!Components.FocusCamera)
        {
            GameObject focusCameraObject = GameObject.Find("PlayerCamera (Focus LockOn)");
            if (focusCameraObject != null)
                Components.FocusCamera = focusCameraObject.GetComponent<CinemachineVirtualCamera>();
        }
        //Components.FocusCamera = FindFirstObjectByType<CinemachineVirtualCamera>().GetComponent<Camera>();
        if (!Components.FocusCamera) Debug.LogError("[PlayerController] PlayerCamera (Focus LockOn) 할당이 되지 않았습니다.");

        // 리지드바디 필수요소 조정.
        if (!Components.Rigidbody.isKinematic)
            Components.Rigidbody.isKinematic = true;

    }
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
    }

    private void FixedStateUpdate() // FixedUpdate가 순서상 우선 // 물리 로직 관련.
    {

    }


    private void StateUpdate()
    {
        // 인풋 상태 처리.
        if (Components.InputHandler)
        {
            Vector2 moveInput = Inputs.MoveVector = Components.InputHandler.Values.MoveVector; // 이동 벡터
            Vector2 lookInput = Inputs.LookVector = Components.InputHandler.Values.LookVector; // 화면 벡터

            Inputs.ScreenCenter = new Vector3(Camera.main.pixelWidth / 2, Camera.main.pixelHeight / 2);
            Inputs.Aim = Camera.main.ScreenPointToRay(Inputs.ScreenCenter); Debug.DrawRay(transform.position, transform.forward * 10f, Color.red);
            
            // 움직이기. (Default : Run / Focus : Walk)
            {
                States.IsMoving = moveInput.sqrMagnitude >= 0.0001f;
                Components.Animator.SetBool("IsMoving", States.IsMoving);

                // 애니메이션 설정.
                Components.Animator.SetFloat("MoveInputX", moveInput.x);
                Components.Animator.SetFloat("MoveInputZ", moveInput.y);
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

                if (Components.OrbitInputProvider)
                    Components.OrbitInputProvider.enabled = !States.IsFocusing;
                else
                    Debug.LogWarning("[PlayerController] OrbitInputProvider가 감지되지 않았습니다. 유의하세요.");

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
        if (Components.IndicatorCanvas)
        {
            bool showIndicator = States.IsFocusing;
            if (Components.IndicatorCanvas.activeSelf != showIndicator)
                Components.IndicatorCanvas.SetActive(showIndicator);
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
    }

    /// <summary>
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

            //Collider bestTarget = FindAimClosetTarget(insideAimEnemies);

            //if (bestTarget == null)
                //bestTarget = FindAimClosetTarget(outsideAimEnemies);
        }

    }

    Collider FindAimClosetTarget(List<Collider> targetColliders)
    {
        if(targetColliders.Count == 0) return null;

        // 가장 가까운 거리의 콜라이더 선택.
        float closestDistanceSqr = float.MaxValue;
        Collider closestEnemy = null;

        // 거리 최소값 콜라이더 구하기
        for (int i = 0; i < targetColliders.Count; ++i)
        {
            Collider detectedEnemy = targetColliders[i];
            if (!detectedEnemy) continue;

            float enemyDistanceSqr = // 거리 계산
                (detectedEnemy.transform.position - Inputs.Aim.origin).sqrMagnitude;

            if (enemyDistanceSqr >= closestDistanceSqr) continue;

            closestDistanceSqr = enemyDistanceSqr;
            closestEnemy = detectedEnemy;
        }

        Values.FocusTarget = closestEnemy;
        States.isEnemyDetected = closestEnemy != null;

        return closestEnemy;
    }

    private void Moving()
    {
        Vector2 moveInput = Inputs.MoveVector;
        Values.MoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Values.MoveAmount = Mathf.Clamp01(Mathf.Abs(moveInput.x) + Mathf.Abs(moveInput.y));
        Values.ForwardVector = Components.Rigidbody.rotation * Vector3.forward; // 플레이어 현재 바라보는 방향
        // Values.ForwardVector = transform.forward; // 플레이어 현재 바라보는 방향

        if (States.IsFocusing)
            MovingFocus();
        else
            MovingDefault();
    }

    private void MovingDefault()
    {
        if (!States.IsMoving)
        {
            States.HasLockedMoveDirection = false;
            Values.PlayerVelocity = Vector3.zero;
            return;
        }

        if (Components.MainCamera == null)
            return;


        // 이동속도.
        float moveSpeed = States.IsSprinting
            ? CheckOptions.SprintSpeed
            : CheckOptions.RunningSpeed;

        // 방향 벡터.
        Vector3 camForward = Components.MainCamera.transform.forward.normalized; // 카메라 전방 벡터.
        Vector3 camRight = Components.MainCamera.transform.right.normalized;     // 카메라 우측 벡터.
        camForward.y = 0f;
        camRight.y = 0f;

        //정지 상태라면.
        if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
        {
            Values.PlayerVelocity = Vector3.zero; return;
        }

        // 현재 사용될 값.
        Vector3 curForward = Values.ForwardVector.normalized; // 단순 플레이어 기준 정방향.
        Vector3 curMoveDir =                                  // 로컬 입력 회전값 (카메라 기준 영향 O).
            (camRight * Values.MoveDirection.x +              // 카메라 좌우.
            camForward * Values.MoveDirection.z)              // 카메라 앞뒤.
            .normalized;

        if (curMoveDir.sqrMagnitude < 0.0001f)
        {
            Values.PlayerVelocity = Vector3.zero;
            return;
        }

        // 바라보는 방향 -> 이동 벡터를 통해 차이나는 각을 획득.
        float angle = Vector3.Angle(curForward, curMoveDir);

        Quaternion targetRotation = Quaternion.LookRotation(curMoveDir); // 가고자 하는 회전 방향 구하기.

        // 현재 회전에서 목표 회전까지,
        // 이번 프레임에 허용된 최대 각도만큼만 회전한 새 Quaternion을 반환.
        Quaternion nextRotation = 
            Quaternion.RotateTowards(
            Components.Rigidbody.rotation,
            targetRotation,
            CheckOptions.RotateSpeed * CheckOptions.RotateSpeed * Time.deltaTime); // 최대 회전각.

        // 회전
        Components.Rigidbody.MoveRotation(nextRotation);

        if (angle > CheckOptions.MaxSlopeAngle)
        {
            if(CheckOptions.TurningMoveSpeed == 0)
            {
                // 제자리 회전만 하기.
                Values.PlayerVelocity = Vector3.zero;
            }
            else
            {
                // 느리게 움직이며 회전하기.
                Vector3 nextPosition = Components.Rigidbody.position + curMoveDir * (CheckOptions.TurningMoveSpeed * Time.deltaTime);
                    Components.Rigidbody.MovePosition(nextPosition);
            }
            return;
        }
        else // 플레이어가 바라보는 각도가 일정 정도까지 다다르면 이동 시작.
        {
            Vector3 nextPosition = Components.Rigidbody.position + curMoveDir * (moveSpeed * Time.deltaTime);
                Components.Rigidbody.MovePosition(nextPosition);
        }
    }

    private void MovingFocus()
    {
        if (Components.MainCamera == null)
            return;

        if (!Values.FocusTarget)
        {
            if (!States.IsMoving)
            {
                Values.PlayerVelocity = Vector3.zero;
                return;
            }

            // 카메라 벡터
            Vector3 camForward = Components.MainCamera.transform.forward.normalized; // 카메라 전방 벡터.
            Vector3 camRight = Components.MainCamera.transform.right.normalized;     // 카메라 우측 벡터.
            camForward.y = 0f;
            camRight.y = 0f;

            Vector3 curMoveDir =                                  // 로컬 입력 회전값 (카메라 기준 영향 O).
            (camRight * Values.MoveDirection.x +                  // 카메라 좌우.
            camForward * Values.MoveDirection.z)                  // 카메라 앞뒤.
            .normalized;

            if (curMoveDir.sqrMagnitude < 0.0001f)
            {
                Values.PlayerVelocity = Vector3.zero;
                return;
            }

            Values.PlayerVelocity = curMoveDir * CheckOptions.WalkSpeed;

            Vector3 nextPosition = Components.Rigidbody.position + Values.PlayerVelocity * Time.deltaTime;
            Components.Rigidbody.MovePosition(nextPosition);
            return;
        }
        else
        {
            Transform focusTarget = Values.FocusTarget.transform;

            // 타겟 기준 현재 위치 오프셋.
            Vector3 offset = Components.Rigidbody.position - focusTarget.position;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.0001f)
                return;

            // 좌우 입력으로 타겟 주위를 원형 회전.
            offset = Quaternion.AngleAxis(-Inputs.MoveVector.x * CheckOptions.RotateSpeed * Time.deltaTime, Vector3.up) * offset;

            // 앞뒤 입력으로 반지름 증감.
            float radius = offset.magnitude;
            radius -= Inputs.MoveVector.y * CheckOptions.WalkSpeed * Time.deltaTime;

            offset = offset.normalized * radius;
            Vector3 nextPosition = focusTarget.position + offset;
            Vector3 lookDir = focusTarget.position - nextPosition;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookDir);

            Values.FocusTargetDistance = radius;

            Components.Rigidbody.MovePosition(nextPosition);
            Components.Rigidbody.MoveRotation(targetRotation);
        }
    }

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
}
