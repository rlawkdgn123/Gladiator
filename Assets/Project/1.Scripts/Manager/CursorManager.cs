//////////////////////////////////////////////////
/// CursorManagger
/// 마우스 입력 관련 클래스.
/// 
///  - 마우스 위치에 따른 3분할.
//////////////////////////////////////////////////
using UnityEngine;
using UnityEngine.InputSystem;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    public enum GuardZone
    {
        None = 0,   // Middle(예외처리 해야함)
        Top,        // Center Top
        Left,       // Bottom Left
        Right,      // Bottom Right
    }

    [Header("Current Zone")]
    public GuardZone CursorZone = GuardZone.None;

    [Header("Settings")]
    [SerializeField] private float m_deadZoneRadius = 40f;
    [SerializeField] private float m_gizmoLineLength = 200f;

    [Header("Input")]
    [SerializeField] private PlayerInputHandler m_inputHandler;

    [SerializeField] private bool m_deBugMode = false;

    private Vector2 screenCenter;
    private Vector2 currentMouseScreenPos;
    private Vector2 currentLookVector;

    private readonly Vector2 m_topDir = new Vector2(0f, 1f);
    private readonly Vector2 m_leftDir = new Vector2(-0.8660254f, -0.5f);   // 240도
    private readonly Vector2 m_rightDir = new Vector2(0.8660254f, -0.5f);   // 300도

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (m_inputHandler == null)
            m_inputHandler = FindFirstObjectByType<PlayerInputHandler>();
    }

    private void Update()
    {
        screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 마우스 공급
        if (m_inputHandler != null)
            currentLookVector = m_inputHandler.Values.LookVector;

        // 마우스 3분할
        if (Mouse.current != null)
        {
            currentMouseScreenPos = Mouse.current.position.ReadValue();
            CursorZone = GetMouseZone(currentMouseScreenPos, m_deadZoneRadius);
        }
    }

    public GuardZone GetMouseZone(Vector2 mouseScreenPos, float radius)
    {
        // 화면 중앙을 기준으로 마우스 방향을 구합니다.
        // 즉 "현재 마우스가 화면 중앙에서 어느 방향을 보고 있느냐"를 판단하기 위한 준비 단계입니다.
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = mouseScreenPos - center;

        // 중앙 근처의 작은 흔들림은 섹터를 확정하지 않습니다.
        // dead zone 안에서는 방향이 계속 바뀌는 현상을 막기 위해 None을 반환합니다.
        if (dir.sqrMagnitude <= radius * radius)
            return GuardZone.None;

        // 내적 비교는 "방향"만 중요하므로 길이를 1로 맞춰줍니다.
        dir.Normalize();

        // Top / Left / Right 세 기준축과 현재 마우스 방향의 내적을 구합니다.
        // 내적값이 클수록 두 벡터가 더 비슷한 방향을 보고 있다는 뜻입니다.
        float topDot = Vector2.Dot(dir, m_topDir);
        float leftDot = Vector2.Dot(dir, m_leftDir);
        float rightDot = Vector2.Dot(dir, m_rightDir);

        // 세 축 중 가장 가까운 축을 현재 마우스 섹터로 판정합니다.
        if (topDot > leftDot && topDot > rightDot)
            return GuardZone.Top;

        if (leftDot > rightDot)
            return GuardZone.Left;

        return GuardZone.Right;
    }

    public Vector2 GetScreenCenter()
    {
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    public Vector2 GetMouseScreenPosition()
    {
        return currentMouseScreenPos;
    }

    public Vector2 GetLookVector()
    {
        return currentLookVector;
    }

    public Vector2 GetTopDir()
    {
        return m_topDir;
    }

    public Vector2 GetLeftDir()
    {
        return m_leftDir;
    }

    public Vector2 GetRightDir()
    {
        return m_rightDir;
    }

    public float GetDeadZoneRadius()
    {
        return m_deadZoneRadius;
    }

    public float GetGizmoLineLength()
    {
        return m_gizmoLineLength;
    }

    public GuardZone GetGuardZone()
    {
        return CursorZone;
    }


    private void OnDrawGizmos()
    {
        if (!m_deBugMode)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 origin = mainCamera.transform.position;

        Vector3 top3D = new Vector3(m_topDir.x, m_topDir.y, 0f).normalized;
        Vector3 left3D = new Vector3(m_leftDir.x, m_leftDir.y, 0f).normalized;
        Vector3 right3D = new Vector3(m_rightDir.x, m_rightDir.y, 0f).normalized;

        Vector2 currentCenter =
            screenCenter == Vector2.zero
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : screenCenter;

        float screenHalfExtent = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) * 0.5f);
        float deadZoneGizmoRadius = (m_deadZoneRadius / screenHalfExtent) * m_gizmoLineLength;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, deadZoneGizmoRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + top3D * m_gizmoLineLength);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + left3D * m_gizmoLineLength);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + right3D * m_gizmoLineLength);

        Vector3 boundaryA = (top3D + right3D).normalized;
        Vector3 boundaryB = (top3D + left3D).normalized;
        Vector3 boundaryC = (left3D + right3D).normalized;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + boundaryA * m_gizmoLineLength);
        Gizmos.DrawLine(origin, origin + boundaryB * m_gizmoLineLength);
        Gizmos.DrawLine(origin, origin + boundaryC * m_gizmoLineLength);

        Vector2 normalizedMouseOffset =
            Vector2.ClampMagnitude((currentMouseScreenPos - currentCenter) / screenHalfExtent, 1f);

        Vector3 mousePointerPos =
            origin + new Vector3(normalizedMouseOffset.x, normalizedMouseOffset.y, 0f) * m_gizmoLineLength;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(origin, mousePointerPos);
        Gizmos.DrawSphere(mousePointerPos, 1f);
    }
}
