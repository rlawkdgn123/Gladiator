using UnityEngine;

public class FocusTester : MonoBehaviour
{
    public PlayerController PlayerController;
    private MeshRenderer m_mesh;
    public Vector3 Offset;
    public bool FindEnemy = false;

    private void Awake()
    {
        m_mesh = GetComponent<MeshRenderer>();
    }
    private void Update()
    {
        FindEnemy = PlayerController.GetFocusTarget();
        m_mesh.enabled = FindEnemy;
        if(FindEnemy)
            transform.position = PlayerController.GetFocusTarget().transform.position + Offset;
    }
}
