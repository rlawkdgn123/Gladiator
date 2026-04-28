/*
 * CameraFollowPivot
 * 피벗의 플레이어 추적을 위한 클래스.
 */
using UnityEngine;

public class CameraFollowPivot : MonoBehaviour
{
    [SerializeField] private Transform m_player;
    [SerializeField] private Vector3 m_offset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private CursorManager m_cursorManager;
    [SerializeField] private PlayerController m_playerController;
    [SerializeField] private float m_panSpeed = 0.15f;
    [SerializeField] private float m_tiltSpeed = 0.15f;
    [SerializeField] private Vector2 m_pitchRange = new Vector2(-70f, 70f);
    [SerializeField] private bool m_invertY;

    private float m_yaw;
    private float m_pitch;

    private void Awake()
    {
        if (m_cursorManager == null)
            m_cursorManager = FindFirstObjectByType<CursorManager>();

        if (m_playerController == null && m_player != null)
            m_playerController = m_player.GetComponent<PlayerController>();

        Vector3 currentEuler = transform.rotation.eulerAngles;
        m_yaw = currentEuler.y;
        m_pitch = NormalizeAngle(currentEuler.x);
    }

    private void FixedUpdate()
    {
        if (m_player == null)
            return;

        transform.position = m_player.position + m_offset;

        if (m_cursorManager == null)
            return;

        if (m_playerController != null
            && m_playerController.GetIsFocusing())
        {
            transform.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
            return;
        }

        Vector2 lookInput = m_cursorManager.GetLookVector();

        m_yaw += lookInput.x * m_panSpeed;

        float pitchDelta = lookInput.y * m_tiltSpeed * (m_invertY ? 1f : -1f);
        m_pitch = Mathf.Clamp(m_pitch + pitchDelta, m_pitchRange.x, m_pitchRange.y);

        transform.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}
