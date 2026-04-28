using GuardIndicatorHUD;
using UnityEngine;

public class PlayerStatusSystem : MonoBehaviour
{
    [SerializeField] private Player.Status m_status = new();

    public Player.Status Stats => m_status;

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
