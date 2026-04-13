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
    }

    private void OnMove(InputValue value)
    {
        Values.MoveVector = value.Get<Vector2>();
    }

    private void OnSprint(InputValue value)
    {
        // 인풋 액션 밸류 가져다씀
    }

    private void OnLook(InputValue value)
    {
        Values.LookVector = value.Get<Vector2>();
    }

    private void OnFocus()
    {
        // 인풋 액션 밸류 가져다씀
        // 마우스 휠클릭 / LCtrl
    }
}
