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
        public PlayerTargetFinder FocusAim;
        public PlayerGuardIndicator GuardIndicator;

        [Space]
        [Header("PlayerCamera")]
        public Camera MainCamera;

        [Space]
        [Header("IndicatorCanvas")]
        public Canvas IndicatorCanvas;
    }

    [Serializable]
    public class Inputs
    {
        public Vector2 MoveVector;
        public Vector2 LookVector;
        public float ScrollY;

        public Vector3 ScreenCenter;
        public Ray Aim;
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

        [Range(1f, 70f), Tooltip("회전 중 이동속도가 정상 속도로 회복되는 각도.")]
        public float MoveSpeedRecoverAngle = 50f;

        [Range(1f, 10f), Tooltip("회전 중 이동속도가 정상 속도로 회복되는 지수.")]
        public float MoveSpeedRecoverPower = 1f;

        [Range(1f, 360f), Tooltip("회전 속도.")]
        public float RotateSpeed = 20f;

        [Range(0f, 30f), Tooltip("주 이동 각도 외 회전 중 시작 이동 속도.")]
        public float TurningMoveSpeed = 1f;

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

        [Range(1f, 30f), Tooltip("포커스 모드 전환을 위한 적 감지 범위.")]
        public float DetectRadius = 2.5f;

        //[Range(1f, 30f), Tooltip("포커스 모드 전환을 위한 에임 적 감지 범위.")]
        //public float AimRadius = 2.5f;

        [Tooltip("카메라/오버랩 프러스텀 최대 감지 범위.")]
        public float DetectAimDistance = 30f;

        [Tooltip("카메라 1차 감지 범위. 이후 검사된 오브젝트를 카메라와 비교합니다.")]
        public Vector3 OverlapBoxHalfExtents = new Vector3(3f, 2f, 6f);
        
        [Tooltip("카메라 1차 감지 범위 위치 보정치.")]
        public Vector3 OverlapBoxForwardOffset = new Vector3(0f, 0f, 0f);

        [Tooltip("포커스 진입 시 가드 인디케이터 페이드아웃 시간.")]
        public float FocusIndicatorFadeOutDuration = 0.2f;

        [Tooltip("포커스 진입 후 타겟이 없을 때 디폴트 모드로 되돌아가기 전 대기 시간.")]
        public float FocusNoTargetReturnDelay = 0.35f;

        [Tooltip("포커스 진입 실패 시 가드 인디케이터 페이드인 시간.")]
        public float FocusIndicatorFadeInDuration = 0.2f;
    }

    [Serializable]
    public class CurrentState
    {

        [Header("States")]
        public bool IsFocusing;
        public bool IsEnteringFocus;
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
        public Vector3 ForwardVector; // 기본 플레이어 전방.
        public CursorManager.GuardZone GuardZone; // 마우스 방향

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
        [Tooltip("포커싱된 상대방과의 거리.")]
        public float FocusTargetDistance;

        [Tooltip("포커스 진입 시 플레이어가 맞춰볼 목표 회전값.")]
        public Quaternion FocusEnterTargetRotation;
    }
}
