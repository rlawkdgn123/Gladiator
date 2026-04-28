using System.Collections.Generic;
using UnityEngine;
using static Global;


public class Weapon : MonoBehaviour, IItem, IWeapon 
{
    enum WeaponType : int
    {
        None = 0,
        SwordShield = 1,
        Max,
    }

    // Status
    [SerializeField] public float Damage;
    [SerializeField] public float ArmorLethality; // 물리 관통력 (상수 방어 관통)
    [SerializeField] public float ArmorPenetration; // 방어구 관통력 (비율 방어 관통)
    [SerializeField] public Global.TeamLayer OwnerTeam; // 방어구 관통력 (비율 방어 관통)

    [DisableField]
    [SerializeField]private Collider m_collider;

    void Awake()
    {
        m_collider = GetComponent<Collider>();
        m_collider.isTrigger = true;

        OwnerTeam = GetTeamFromLayer(gameObject.layer);
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        
    }
    public static TeamLayer GetTeamFromLayer(int layer)
    {
        if (layer == PlayerLayer)
            return TeamLayer.Player;

        if (layer == EnemyLayer)
            return TeamLayer.Enemy;

        return TeamLayer.None;
    }

    public void GetDamage()
    {

    }

    public void SetWeaponCollider(int value)
    {
        m_collider.enabled = (value != 0);
    }
    public void SetWeaponCollider(bool enabled)
    {
        m_collider.enabled = enabled;
    }
    public void SetWeaponCollider(string command)
    {
        if (Global.OnCommand.Contains(command))
            m_collider.enabled = true;
        else
            m_collider.enabled = false;
    }
}
