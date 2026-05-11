using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using VInspector;

namespace PlayerControllerInfo
{
    [Serializable]
    public class Components
    {
        [Foldout("Player")]
        public Animator Animator;
        public Rigidbody Rigidbody;
        public CapsuleCollider CapsuleCollider;
        public PlayerInput PlayerInput;
        public PlayerInputHandler InputHandler;
        public PlayerTargetFinder FocusAim;
        public PlayerGuardIndicatorSystem PlayerGuardIndicatorSystem;
        public PlayerStatusSystem PlayerStatus;
        public Weapon PlayerWeapon;

        [Space]
        [Header("Manager")]
        public CursorManager CursorManager;

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
        public GuardZone GuardZone;
    }

    [Serializable]
    public class CheckOption
    {
        [Space]
        [Foldout("GroundCheck")]
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

        [Space]
        [Foldout("Rotate")]
        [Range(1f, 90f), Tooltip("카메라 기준 목표 이동 방향에 충분히 가까워졌다고 판단하는 각도. 이 값 이하부터 최대 이동속도를 유지.")]
        public float FullSpeedStartAngle = 50f;
        [Range(1f, 10f), Tooltip("회전 중 이동속도가 정상 속도로 회복되는 지수. 높을수록 빠르고 낮을수록 묵직함.")]
        public float MoveSpeedRecoverPower = 1f;
        [Range(1f, 30f), Tooltip("애니메이터 이동 Amount 보간 속도.")]
        public float MoveAmountLerpSpeed = 10f;
        [Range(1f, 30f), Tooltip("포커스 모드 애니메이터 이동 입력 보간 속도.")]
        public float FocusMoveInputLerpSpeed = 12f;
        [Range(1f, 30f), Tooltip("디폴트 모드 이동 방향 보간 속도.")]
        public float DefaultMoveDirectionLerpSpeed = 12f;
        [Range(0.01f, 10f), Tooltip("포커스 타겟 전환 시 바라보는 기준점 보간 속도.")]
        public float FocusTargetLerpSpeed = 0.15f;
        [Range(0.1f, 10f), Tooltip("포커스 모드에서 타겟에게 더 이상 접근하지 않을 최소 거리.")]
        public float FocusMinDistance = 1.5f;




        [Foldout("Gravity")]
        [Tooltip("중력.")]
        public float Gravity = -9.81f;

        [Foldout("OverlapBox")]
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
        [Tooltip("디버그용. 켜면 OverlapBox가 Enemy 레이어 대신 모든 레이어를 대상으로 검사합니다.")]
        public bool DebugDetectAllLayers = false;

        [Foldout("Indicator")]
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

        [Foldout("States")]
        public bool IsFocusing;
        public bool IsEnteringFocus;
        public bool isEnemyDetected;
        public bool IsPerformingAction;
        public bool IsForwardBlocked;
        public bool IsGrounded;
        public bool IsOnSteepSlope;
        public bool CanGuard; // Trigger 행동을 하지 않을 때
        public bool CanNextAttack;
        public bool IsAttackReceive; // 어떤 상태든간 피격받을 때

        [Space]
        [Foldout("Move")]
        public bool IsMoving;
        public bool IsSprinting;
        public bool IsStrafing;
        public bool HasLockedMoveDirection;

        [Space]
        [Foldout("TriggerAction")]
        public bool IsAttacking;
        public bool IsHitting;
        public bool IsParrying;
        public bool IsGuarding;
        
    }

    [Serializable]
    public class CurrentValue
    {
        [Header("Movement")]
        public float MoveAmount;
        public float TargetMoveAmount;
        public float MoveInputX;
        public float MoveInputZ;
        public Vector3 MoveDirection;
        public Vector3 DefaultMoveDirection;
        public Vector3 GroundNormal;
        public Vector3 GroundCross;
        public Vector3 PlayerVelocity;
        [Space]

        [Header("Direction")]
        [Tooltip("기본적인 플레이어의 전방.")]
        public Vector3 ForwardVector; // 기본 플레이어 전방.
        public Vector3 FocusNoTargetForward;
        public Vector3 FocusNoTargetRight;
        public GuardZone GuardZone; // 마우스 방향

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
        public List<Collider> AimEnemysBuffer = new();
        [Tooltip("포커싱된 상대방과의 거리.")]
        public float FocusTargetDistance;
        public Vector3 FocusTargetPoint;
        public Collider PreviousFocusTarget;

        [Tooltip("포커스 진입 시 플레이어가 맞춰볼 목표 회전값.")]
        public Quaternion FocusEnterTargetRotation;
    }

    [Serializable]
    public class Status
    {
        /*[Header("Health")]
        [Range(0f, 99999f), Tooltip("플레이어 최대 체력.")]
        public float MaxHealth;
        [Range(1f, 99999f), Tooltip("플레이어 현재 체력.")]
        public float CurrentHealth;*/

        [Space]
        [Header("Stamina")]
        [Range(0f, 99999f), Tooltip("플레이어 최대 스태미나.")]
        public float MaxStamina;
        [Range(1f, 99999f), Tooltip("플레이어 현재 스태미나.")]
        public float CurrentStamina;

        [Space]
        [Header("Speed")]
        [Range(1f, 500f), Tooltip("공격 속도.")]
        public float AttackSpeed = 220f;
        [Range(1f, 360f), Tooltip("회전 속도.")]
        public float RotateSpeed = 220f;
        [Range(0f, 30f), Tooltip("주 이동 각도 외 회전 중 시작 이동 속도.")]
        public float TurningMoveSpeed = 5f;
        [Range(1f, 30f), Tooltip("전력질주 속도. [DefaultMode]")]
        public float SprintSpeed = 9f;
        [Range(1f, 30f), Tooltip("달리기 속도. [DefaultMode]")]
        public float RunningSpeed = 6f;
        [Range(1f, 30f), Tooltip("걷기 속도. [FocusMode]")]
        public float WalkSpeed = 2.5f;
        //[Tooltip("경사면 이동 가속도 배율.")]
        //public float SlopeAcceleration = 1f;
    }
}
