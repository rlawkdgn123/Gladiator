using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerComponents m_playerComponents = new();
    [SerializeField] private PlayerInputs m_playerInputs = new();
    [SerializeField] private CheckOption m_checkOption = new();
    [SerializeField] private CurrentState m_currentState = new();
    [SerializeField] private CurrentValue m_currentValue = new();
    [SerializeField] private PlayerFollowCamera m_playerFollowCamera = new();

    private PlayerComponents Components => m_playerComponents;
    private PlayerInputs Inputs => m_playerInputs;
    private CheckOption CheckOptions => m_checkOption;
    private CurrentState States => m_currentState;
    private CurrentValue Values => m_currentValue;
    private PlayerFollowCamera FollowCamera => m_playerFollowCamera;

    private void Awake()
    {
        if (Components.Animator == null)
        {
            Components.Animator = GetComponent<Animator>();
        }

        if (Components.Rigidbody == null)
        {
            Components.Rigidbody = GetComponent<Rigidbody>();
        }

        if (Components.CapsuleCollider == null)
        {
            Components.CapsuleCollider = GetComponent<CapsuleCollider>();
        }
    }

    private void FixedUpdate()
    {
        StateCheck();
        Walking();
    }

    private void StateCheck()
    {
    }

    private void Walking()
    {
        Vector2 moveInput = Inputs.MoveVector;
        Values.MoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Values.MoveAmount = Mathf.Clamp01(Mathf.Abs(moveInput.x) + Mathf.Abs(moveInput.y)); // 절댓값 보정

        if (States.IsFocusing) // 포커스 모드이면
            WalkingFocus();
        else
            WalkingDefault();
    }

    // 자유 시점 이동 입력을 월드 방향으로 정리한다.
    private void WalkingDefault()
    {
        if (States.IsFocusing) return;

        // 기본(달리기) / 전력질주 속도 할당
        float moveDistance = 0.0f;

        if (States.IsRunning)
            moveDistance = CheckOptions.RunningSpeed * Time.deltaTime;
        else if (States.IsSprinting)
            moveDistance = CheckOptions.SprintSpeed * Time.deltaTime;

        transform.position += Values.MoveDirection * moveDistance;

    }

    // 포커스 이동 로직은 이후 별도 구현한다.
    private void WalkingFocus()
    {
        WalkingDefault(); // 임시
        return;

        /////// 추후에 연결할 부분 //////
        if (!States.IsFocusing) return;

        float moveDistance = CheckOptions.WalkSpeed * Time.deltaTime;

        transform.position += Values.MoveDirection * moveDistance;

    }

    private void OnMove(InputValue value)
    {
        if (value.isPressed)
        {
            States.IsNotMoving = false;
        }
        else 
        {
            States.IsNotMoving = true;
        }

            Inputs.MoveVector = value.Get<Vector2>(); // x 좌우, y 앞뒤
    }
}