using GuardIndicatorSystemInfo;
using UnityEngine;

public class PlayerStatusSystem : MonoBehaviour
{
    [SerializeField] private PlayerControllerInfo.Status m_status = new();

    public PlayerControllerInfo.Status Stats => m_status;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void TakeDamage(float damage)
    {
        //Stats.CurrentHealth -= damage;
    }
}
