/*
 * PlayerCameraSystem
 * !!!!!미완성 주의!!!!! 
 * FreeLook Camera 컴포넌트로 일단 사용하고, 
 * 나중에 바꿀 일이 있다면 해당 스크립트를 추가 개발하기로 함.
 * !!!!!미완성 주의!!!!! 
*/

using System;
using UnityEditor;
using UnityEngine;

namespace PlayerCamera
{
    [Serializable]
    public class Components
    {
        [Header("Player")]
        public GameObject Player;
        [Header("Camera Position")]
        public Transform CamPositionDefault;
        public Transform CamPositionLeft;
        public Transform CamPositionRight;
    }

    [Serializable]
    public class CheckOption
    {
        [Header("Value")]
        public Vector3 CameraRotation;
        public float HorizontalSensitivity = 500f;
        public float VerticalSensitivity = 500f;
        public float MinDistance = -35f;
        public float MaxDistance = 35f;
        [Space]
        [Header("Camera Debug")]
        public Vector3 CameraFollowVelocity = Vector3.zero;
        public float LeftRightLookAngle;
        public float UpDownLookAngle;
        public float radius = 3f;
    }
}

public class PlayerCameraSystem : MonoBehaviour
{
    [SerializeField] private PlayerCamera.Components m_components = new();
    [SerializeField] private PlayerCamera.CheckOption m_checkOption = new();
    [SerializeField] private Camera m_camera;
    [SerializeField] private PlayerInputHandler m_inputHandler;

    private PlayerCamera.Components Components => m_components;
    private PlayerCamera.CheckOption CheckOptions => m_checkOption;

    private void Awake()
    {
        // 카메라 포지션 바인딩
        if (!Components.CamPositionDefault)
        {
            Components.CamPositionDefault = GameObject.Find("DefaultCameraPosition").transform;
            if (!Components.CamPositionDefault)
                Debug.LogWarning("[PlayerCameraSystem] 기본 시점 카메라 DefaultCamPosition이 할당되지 않았습니다.");
        }


        if (!Components.CamPositionLeft)
        {
            Components.CamPositionLeft = GameObject.Find("CameraPosition(L)").transform;
            if (!Components.CamPositionLeft)
                Debug.LogWarning("[PlayerCameraSystem] 기본 시점 카메라 DefaultCamPosition이 할당되지 않았습니다.");
        }
          

        if (!Components.CamPositionRight)
        {
            Components.CamPositionRight = GameObject.Find("CameraPosition(R)").transform;
            if (!Components.CamPositionRight)
                Debug.LogWarning("[PlayerCameraSystem] 기본 시점 카메라 DefaultCamPosition이 할당되지 않았습니다.");
        }
        
        if (m_inputHandler == null)
            m_inputHandler = FindAnyObjectByType<PlayerInputHandler>();

        if (Components.Player == null && m_inputHandler != null)
            Components.Player = m_inputHandler.gameObject;

        if (m_camera == null)
            m_camera = GetComponent<Camera>();

        if (m_camera != null)
        {
            CheckOptions.LeftRightLookAngle = NormalizeAngle(transform.eulerAngles.y);
            CheckOptions.UpDownLookAngle = NormalizeAngle(transform.eulerAngles.x);
            CheckOptions.CameraRotation = transform.eulerAngles;
        }
    }
    private void Update()
    {
    }

    private void LateUpdate()
    {
        CameraLateUpdate();
    }

    private void OnDrawGizmos()
    {
        Handles.color = Color.red;
        Handles.DrawWireDisc(Components.Player.transform.position, Vector3.up, CheckOptions.radius);
    }

    private void CameraLateUpdate()
    {
        if (m_inputHandler == null || m_camera == null)
            return;

        if (Components.CamPositionDefault != null)
            transform.position = Components.CamPositionDefault.position;

        if (Components.CamPositionLeft != null)
            transform.position = Components.CamPositionLeft.position;

        if (Components.CamPositionRight != null)
            transform.position = Components.CamPositionRight.position;

        Vector2 lookInput = m_inputHandler.Values.LookVector;

        CheckOptions.LeftRightLookAngle += lookInput.x * CheckOptions.HorizontalSensitivity * Time.deltaTime;
        CheckOptions.UpDownLookAngle -= lookInput.y * CheckOptions.VerticalSensitivity * Time.deltaTime;
        CheckOptions.UpDownLookAngle = Mathf.Clamp(CheckOptions.UpDownLookAngle, CheckOptions.MinDistance, CheckOptions.MaxDistance);

        transform.rotation = Quaternion.Euler(CheckOptions.UpDownLookAngle, CheckOptions.LeftRightLookAngle, 0f);
        CheckOptions.CameraRotation = transform.eulerAngles;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
