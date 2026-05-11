using UnityEngine;

public class PositionDebuger : MonoBehaviour
{
    [Header("Components")]
    public Transform player;
    public Transform FollowTarget;
    public Transform MainCamera;

    [Header("Position(Fixed)")]
    public Vector3 playerFixedPos;
    public Vector3 FollowTargetFixedPos;
    public Vector3 MainCameraFixedPos;

    [Header("Rotation(Fixed)")]
    public Vector3 playerFixedRot;
    public Vector3 FollowTargetFixedRot;
    public Vector3 MainCameraFixedRot;

    [Header("Position")]
    public Vector3 playerPos;
    public Vector3 FollowTargetPos;
    public Vector3 MainCameraPos;

    [Header("Rotation")]
    public Vector3 playerRot;
    public Vector3 FollowTargetRot;
    public Vector3 MainCameraRot;

    void FixedUpdate()
    {
        playerFixedPos = player.position;
        FollowTargetFixedPos = FollowTarget.position;
        MainCameraFixedPos = MainCamera.position;

        playerFixedRot = player.rotation.eulerAngles;
        FollowTargetFixedRot = FollowTarget.rotation.eulerAngles;
        MainCameraFixedRot = MainCamera.rotation.eulerAngles;
    }

    void Update()
    {
        playerPos = player.position;
        FollowTargetPos = FollowTarget.position;
        MainCameraPos = MainCamera.position;

        playerRot = player.rotation.eulerAngles;
        FollowTargetRot = FollowTarget.rotation.eulerAngles;
        MainCameraRot = MainCamera.rotation.eulerAngles;
    }
}
