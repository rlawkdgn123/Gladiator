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
        public InputAction WheelAction;
    }
}


[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerInput m_playerInput;


    [SerializeField] private InputHandle.InputValues m_inputValues = new();
    [SerializeField] private InputHandle.InputActions m_inputActions = new();

    private InputValues Values => m_inputValues;
    private InputActions Actions => m_inputActions;

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
            Actions.WheelAction = m_playerInput.actions["ChangeTarget"];
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
        //Debug.Log("OnAttack");
    }

    private void OnParry(InputValue value)
    {
        Debug.Log("OnParry");
    }

    private void OnChangeTarget(InputValue value)
    {
        Values.ScrollY = value.Get<float>();
        //Debug.Log("OnChangeTarget");
        //Debug.Log(Values.ScrollY);
    }

    public Vector2 GetMoveVector()
    {
        return Values.MoveVector;
    }

    public Vector2 GetLookVector()
    {
        return Values.LookVector;
    }

    public float GetScrollY()
    {
        return Values.ScrollY;
    }

    public void SetScrollY(float scrollY)
    {
        Values.ScrollY = scrollY;
    }

    public bool GetSprintActionIsPressed()
    {
        return Actions.SprintAction != null
            && Actions.SprintAction.IsPressed();
    }

    public bool GetFocusActionWasPressedThisFrame()
    {
        return Actions.FocusAction != null
            && Actions.FocusAction.WasPressedThisFrame();
    }

    public bool GetAttackActionWasPressedThisFrame()
    {
        return Actions.AttackAction != null
            && Actions.AttackAction.WasPressedThisFrame();
    }

    public bool GetParryActionWasPressedThisFrame()
    {
        return Actions.ParryAction != null
            && Actions.ParryAction.WasPressedThisFrame();
    }

    public bool GetWheelActionWasPressedThisFrame()
    {
        return Actions.WheelAction != null
            && Actions.WheelAction.WasPressedThisFrame();
    }
}
