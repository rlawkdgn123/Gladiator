using System;
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
        if (Components.Animator == null)
            Components.Animator = GetComponent<Animator>();

        if (Components.Rigidbody == null)
            Components.Rigidbody = GetComponent<Rigidbody>();

        if (Components.CapsuleCollider == null)
            Components.CapsuleCollider = GetComponent<CapsuleCollider>();

        if (Components.PlayerInput == null)
            Components.PlayerInput = GetComponent<PlayerInput>();

        if (Components.InputHandler == null)
            Components.InputHandler = GetComponent<PlayerInputHandler>();

        if (Components.IndicatorCanvas == null)
        {
            PlayerGuardIndicator indicator = GetComponentInChildren<PlayerGuardIndicator>();
            if (indicator != null)
                Components.IndicatorCanvas = indicator.gameObject;
        }

        if (Components.Camera == null)
            Components.Camera = GetComponentInChildren<Camera>(true);

        if (Components.Camera == null)
            Components.Camera = Camera.main;

        if (!Components.Rigidbody.isKinematic)
            Components.Rigidbody.isKinematic = true;
    }
    private void FixedUpdate()
    {
        FixedStateUpdate();     // 플레이어 상태 업데이트 [Fixed]
        Moving();               // 플레이어 이동 분기
    }

    private void Update()
    {
        StateUpdate();          // 플레이어 상태 업데이트 [Frame]
        GuardIndicatorUpdate(); // 가드 인디케이터 상태 처리
    }


    private void FixedStateUpdate() // FixedUpdate가 순서상 우선 // 물리 로직 관련
    {

    }

    private void StateUpdate()
    {
        // 인풋 상태 처리
        if (Components.InputHandler)
        {
            Inputs.MoveVector = Components.InputHandler.Values.MoveVector; // 이동 벡터
            Inputs.LookVector = Components.InputHandler.Values.LookVector; // 화면 벡터

            // 움직이기 (Default : Run / Focus : Walk)
            {
                States.IsMoving = Inputs.MoveVector.sqrMagnitude >= 0.0001f;
                Components.Animator.SetBool("IsMoving", States.IsMoving);
            }
            
            States.IsStrafing = Mathf.Abs(Inputs.MoveVector.x) > 0.0001f;

            // 전력질주
            {
                States.IsSprinting = Components.InputHandler.Actions.SprintAction != null
                && Components.InputHandler.Actions.SprintAction.IsPressed();
                Components.Animator.SetBool("IsSprinting", States.IsSprinting);
            }


            if (Components.InputHandler.Actions.FocusAction != null
                && Components.InputHandler.Actions.FocusAction.WasPressedThisFrame())
            {
                States.IsFocusing = !States.IsFocusing;
                Components.Animator.SetBool("IsFocusing", States.IsFocusing);
            }
                
        }
        else Debug.LogError("[PlayerController] InputHandler 필요.");

        // 인풋 조건 처리
        {
            if (States.IsFocusing)
            {
                if(!Values.FocusTarget) DetectEnemy();
            }
            else
            {
                // 초기화
                Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);
                Values.FocusTarget = null;
                States.isEnemyDetected = false;
            }
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

    private void DetectEnemy()
    {
        Array.Clear(Values.DetectEnemysBuffer, 0, Values.DetectEnemysBuffer.Length);
        int detectEnemyCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            CheckOptions.DetectRadius,
            Values.DetectEnemysBuffer,
            Global.EnemyLayerMask);

        // 입력된 인원 개수 카운트
        if (detectEnemyCount <= 0)
        {
            States.isEnemyDetected = false;
            Values.FocusTarget = null;
            return;
        }

        
        // 가장 가까운 거리의 콜라이더 선택
        float closestDistanceSqr = float.MaxValue;
        Collider closestEnemy = null;

        for (int i = 0; i < detectEnemyCount; i++)
        {
            Collider detectedEnemy = Values.DetectEnemysBuffer[i];
            if (detectedEnemy == null)
                continue;

            float enemyDistanceSqr = (detectedEnemy.transform.position - transform.position).sqrMagnitude;
            if (enemyDistanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = enemyDistanceSqr;
            closestEnemy = detectedEnemy;
        }

        Values.FocusTarget = closestEnemy;
        States.isEnemyDetected = closestEnemy != null;
    }

    private void Moving()
    {
        Vector2 moveInput = Inputs.MoveVector;
        Values.MoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Values.MoveAmount = Mathf.Clamp01(Mathf.Abs(moveInput.x) + Mathf.Abs(moveInput.y));
        Values.ForwardVector = Components.Rigidbody.rotation * Vector3.forward; // 플레이어 현재 바라보는 방향
        //Values.ForwardVector = transform.forward; // 플레이어 현재 바라보는 방향

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

        if (Components.Camera == null)
            return;


        // 이동속도
        float moveSpeed = States.IsSprinting
            ? CheckOptions.SprintSpeed
            : CheckOptions.RunningSpeed;

        // 방향 벡터
        Vector3 camForward = Components.Camera.transform.forward.normalized; // 카메라 전방 벡터
        Vector3 camRight = Components.Camera.transform.right.normalized;     // 카메라 우측 벡터
        camForward.y = 0f;
        camRight.y = 0f;

        //정지면
        if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
        {
            Values.PlayerVelocity = Vector3.zero; return;
        }

        // 현재 사용될 값
        Vector3 curForward = Values.ForwardVector.normalized; // 단순 플레이어 기준 정방향
        Vector3 curMoveDir =                                  // 로컬 입력 회전값 (카메라 기준 영향 O)
        (camRight * Values.MoveDirection.x +                  // 카메라 좌우
        camForward * Values.MoveDirection.z)                  // 카메라 앞뒤
        .normalized;

        if (curMoveDir.sqrMagnitude < 0.0001f)
        {
            Values.PlayerVelocity = Vector3.zero;
            return;
        }

        // 바라보는 방향 -> 이동 벡터를 통해 차이나는 각을 획득
        float angle = Vector3.Angle(curForward, curMoveDir);

        Quaternion targetRotation = Quaternion.LookRotation(curMoveDir); // 가고자 하는 회전 방향 구하기

        // 현재 회전에서 목표 회전까지,
        // 이번 프레임에 허용된 최대 각도만큼만 회전한 새 Quaternion을 반환
        Quaternion nextRotation = Quaternion.RotateTowards(
        Components.Rigidbody.rotation,
        targetRotation,
        CheckOptions.RotationSpeed * CheckOptions.RotationSpeed * Time.deltaTime); // 최대 회전각

        // 회전
        Components.Rigidbody.MoveRotation(nextRotation);

        if (angle > CheckOptions.MaxSlopeAngle)
        {
            // 많이 틀어져 있으면 제자리 회전만

            Vector3 nextPosition = Components.Rigidbody.position + curMoveDir * (1 * Time.deltaTime);
            Components.Rigidbody.MovePosition(nextPosition);

            //Values.PlayerVelocity = Vector3.zero;
            return;
        }
        else // 플레이어가 바라보는 각도가 일정 정도까지 다다르면 이동 시작
        {
            Vector3 nextPosition = Components.Rigidbody.position + curMoveDir * (moveSpeed * Time.deltaTime);
            Components.Rigidbody.MovePosition(nextPosition);
        }
    }

    private void MovingFocus()
    {
        MovingDefault();
    }

    private void OnDrawGizmos()
    {
        if (States.IsFocusing)
        {
            Color radiusColor = States.isEnemyDetected ? Color.orangeRed : Color.olive;
            Handles.color = radiusColor;
            Handles.DrawWireDisc(transform.position, Vector3.up, CheckOptions.DetectRadius);
        }
    }
}
