/*
 * PlayerDefinitions.cs
 * 플레이어 관련 데이터 정의
 */

using System;
using UnityEngine;

[Serializable]
public class PlayerComponents
{
    public Animator Animator;
    public Rigidbody Rigidbody;
    public CapsuleCollider CapsuleCollider;
}

[Serializable]
public class PlayerInputs
{
    public Vector2 MoveVector;
    public Vector2 LookVector;
}

[Serializable]
public class CheckOption
{
    [Tooltip("지면으로 체크할 레이어 설정")]
    public LayerMask GroundLayerMask = -1;

    [Range(0.01f, 0.05f), Tooltip("전방 장애물 감지 거리")]
    public float ForwardCheckDistance = 0.1f;

    [Range(0.1f, 10.0f), Tooltip("지면 감지 거리")]
    public float GroundCheckDistance = 2.0f;

    [Range(0.0f, 0.1f), Tooltip("지면 감지 보정값")]
    public float GroundCheckThreshold = 0.01f;

    [Range(1f, 70f), Tooltip("등반이 가능한 경사각")]
    public float MaxSlopeAngle = 50f;

    [Range(1f, 20f), Tooltip("회전 속도")]
    public float RotationSpeed = 20f;

    [Range(1f, 30f), Tooltip("전력 질주 속도")]
    public float SprintSpeed = 9f;

    [Range(1f, 30f), Tooltip("달리기(기본) 속도")]
    public float RunningSpeed = 6f;

    [Range(1f, 30f), Tooltip("걷기(포커스) 속도")]
    public float WalkSpeed = 2.5f;

    [Tooltip("경사로 이동속도 변화율(가속/감속)")]
    public float SlopeAcceleration = 1f;

    [Tooltip("중력")]
    public float Gravity = -9.81f;
}

[Serializable]
public class CurrentState
{
    public bool IsFocusing;

    [Space]
    public bool IsNotMoving;
    public bool IsWalking;
    public bool IsRunning;
    public bool IsSprinting;
    public bool IsStrafing;

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
    public float MoveAmount; // 입력 여부
    public Vector3 MoveDirection; // 움직임 방향
    public Vector3 GroundNormal;
    public Vector3 GroundCross;
    public Vector3 PlayerVelocity;

    [Space]
    public float GroundDistance;
    public float GroundSlopeAngle;
    public float ForwardSlopeAngle;
    public float SlopeAcceleration;

    [Space]
    public float Gravity;
}

[Serializable]
public class PlayerFollowCamera
{
    [Header("Object")]
    public GameObject PlayerCamera;
    public GameObject PlayerCameraPivot;
    public Camera Camera;

    [Header("Value")]
    public float LeftRightLookSpeed = 500f;
    public float UpDownLookSpeed = 500f;
    public float MinPivot = -35f;
    public float MaxPivot = 35f;

    [Header("Camera Debug")]
    public Vector3 CameraFollowVelocity = Vector3.zero;
    public float LeftRightLookAngle;
    public float UpDownLookAngle;
}