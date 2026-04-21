using InputHandle;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputHandle
{
    [Serializable]
    public class InputValues
    {
        public Vector2 MoveVector;
        public Vector2 LookVector;
        public float ScrollY;
    }

    // 미사용
    public class InputButtons
    {
        //public bool LeftShift;
    }

    [Serializable]
    public class InputActions
    {
        public InputAction SprintAction;
        public InputAction FocusAction;
        public InputAction AttackAction;
        public InputAction ParryAction;
        public InputAction WhillAction;
    }
}


[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerInput m_playerInput;


    [SerializeField] private InputHandle.InputValues m_inputValues = new();
    [SerializeField] private InputHandle.InputActions m_inputActions = new();

    public InputValues Values => m_inputValues;
    public InputActions Actions => m_inputActions;

    private void Awake()
    {
        if (m_playerInput == null)
            m_playerInput = GetComponent<PlayerInput>();

        if (m_playerInput != null)
            Actions.SprintAction = m_playerInput.actions["Sprint"];

        if (m_playerInput != null)
            Actions.FocusAction = m_playerInput.actions["Focus"];

        if (m_playerInput != null)
            Actions.AttackAction = m_playerInput.actions["Attack"];

        if (m_playerInput != null)
            Actions.ParryAction = m_playerInput.actions["Parry"];

        if (m_playerInput != null)
            Actions.ParryAction = m_playerInput.actions["ChangeTarget"];
    }

    private void OnMove(InputValue value)
    {
        //Debug.Log("OnMove");
        Values.MoveVector = value.Get<Vector2>();
    }

    private void OnLook(InputValue value)
    {
        //Debug.Log("OnLook");
        Values.LookVector = value.Get<Vector2>();
    }

    private void OnSprint(InputValue value)
    {
        Debug.Log("OnSprint");
        // 인풋 액션 밸류 가져다씀
    }

    private void OnFocus(InputValue value)
    {
        Debug.Log("OnFocus");
        // 인풋 액션 밸류 가져다씀
        // 마우스 휠클릭 / LCtrl
    }

    private void OnAttack(InputValue value)
    {
        Debug.Log("OnAttack");
    }

    private void OnParry(InputValue value)
    {
        Debug.Log("OnParry");
    }

    private void OnChangeTarget(InputValue value)
    {
        Values.ScrollY = value.Get<float>();
        Debug.Log("OnChangeTarget");
        Debug.Log(Values.ScrollY);
    }
}
