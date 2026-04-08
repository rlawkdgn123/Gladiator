using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [Serializable]
    public class Components
    {
        [Header("Player")]
        public Animator Animator;
        public Rigidbody Rigidbody;
        public CapsuleCollider CapsuleCollider;
        public PlayerInput PlayerInput;
        public PlayerInputHandler InputHandler;

        [Space]
        [Header("PlayerCamera")]
        public Camera Camera;

        [Space]
        [Header("IndicatorCanvas")]
        public GameObject IndicatorCanvas;
    }

    [Serializable]
    public class Inputs
    {
        public Vector2 MoveVector;
        public Vector2 LookVector;
    }

    [Serializable]
    public class CheckOption
    {
        [Tooltip("지면 체크에 사용할 레이어 마스크.")]
        public LayerMask GroundLayerMask = -1;

        [Range(0.01f, 0.05f), Tooltip("전방 장애물 체크 거리.")]
        public float ForwardCheckDistance = 0.1f;

        [Range(0.1f, 10.0f), Tooltip("지면 체크 거리.")]
        public float GroundCheckDistance = 2.0f;

        [Range(0.0f, 0.1f), Tooltip("지면 체크 판정 임계값.")]
        public float GroundCheckThreshold = 0.01f;

        [Range(1f, 70f), Tooltip("이동 가능한 최대 경사 각도.")]
        public float MaxSlopeAngle = 50f;

        [Range(1f, 20f), Tooltip("회전 속도.")]
        public float RotationSpeed = 20f;

        [Range(1f, 30f), Tooltip("질주 속도.")]
        public float SprintSpeed = 9f;

        [Range(1f, 30f), Tooltip("기본 달리기 속도.")]
        public float RunningSpeed = 6f;

        [Range(1f, 30f), Tooltip("걷기 속도.")]
        public float WalkSpeed = 2.5f;

        [Tooltip("경사면 이동 가속도 배율.")]
        public float SlopeAcceleration = 1f;

        [Tooltip("중력.")]
        public float Gravity = -9.81f;

        [Range(1f, 30f), Tooltip("포커스 모드 적 감지 범위.")]
        public float DetectRadius = 2.5f;
    }

    [Serializable]
    public class CurrentState
    {
        public bool IsFocusing;
        public bool isEnemyDetected;

        [Space]
        public bool IsMoving;
        public bool IsSprinting;
        public bool IsStrafing;
        public bool HasLockedMoveDirection;

        [Space]
        public bool IsPerformingAction;

        [Space]
        public bool IsForwardBlocked;
        public bool IsGrounded;
        public bool IsOnSteepSlope;

        
    }

    [Serializable]
    public class CurrentValue
    {
        [Header("Movement")]
        public float MoveAmount;
        public Vector3 MoveDirection;
        public Vector3 GroundNormal;
        public Vector3 GroundCross;
        public Vector3 PlayerVelocity;
        [Space]

        [Header("Direction")]
        [Tooltip("기본적인 플레이어의 전방.")]
        public Vector3 ForwardVector; // 기본 플레이어 전방

       [Space]
        public float GroundDistance;
        //public float GroundSlopeAngle;
        //public float ForwardSlopeAngle;
        //public float SlopeAcceleration;

        [Space]
        public float Gravity;

        [Space]
        [Header("Detect")]
        public Collider FocusTarget;
        public Collider[] DetectEnemysBuffer = new Collider[Global.MaxPlayersPerTeam];

    }

    [Serializable]
    public class PlayerFollowCamera
    {
        [Header("Object")]
        public GameObject Player;
        public GameObject PlayerCamera;
        public GameObject PlayerCameraPivot;

        [Header("Value")]
        public Vector3 CameraRotation;
        public float LeftRightLookSpeed = 500f;
        public float UpDownLookSpeed = 500f;
        public float MinDistance = -35f;
        public float MaxDistance = 35f;

        [Header("Camera Debug")]
        public Vector3 CameraFollowVelocity = Vector3.zero;
        public float LeftRightLookAngle;
        public float UpDownLookAngle;
    }
}
