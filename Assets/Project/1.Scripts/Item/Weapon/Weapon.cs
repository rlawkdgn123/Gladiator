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
    [SerializeField] public Global.TeamLayer OwnerTeam; // 소유자의 팀

    [DisableField]
    [SerializeField] private Collider m_collider;

    [DisableField]
    [SerializeField] readonly private HashSet<GameObject> m_hitRoots = new();

    void Awake()
    {
        m_collider = GetComponent<Collider>();
        m_collider.isTrigger = true;

        OwnerTeam = ResolveOwnerTeam();

        if (OwnerTeam == Global.TeamLayer.None)
            Debug.LogWarning($"[Weapon] {name}의 OwnerTeam을 찾지 못했습니다. self:{gameObject.layer}, root:{transform.root.gameObject.layer}", this);
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log(other.name + "콜라이더 충돌!!!");
        GameObject otherRoot = other.transform.root.gameObject;
        int otherLayer = otherRoot.layer;

        switch (OwnerTeam) // 소유자 검증
        { 
        case Global.TeamLayer.Player:
            {
                if (otherLayer == Global.EnemyLayer)
                {
                    if (!m_hitRoots.Add(otherRoot)) return;

                        Debug.Log("플레이어 " + otherRoot.name + "의 검이" + other.name + "을 타격함");
                }
            }break;
        case Global.TeamLayer.Enemy:
            {
                if (otherLayer == Global.PlayerLayer)
                {
                    if (!m_hitRoots.Add(otherRoot)) return;

                    Debug.Log("적 " + otherRoot.name + "의 검이" + other.name + "을 타격함");
                }
            }break;
        }
    }

    public static TeamLayer GetTeamFromLayer(int layer)
    {
        if (layer == PlayerLayer)
            return TeamLayer.Player;

        if (layer == EnemyLayer)
            return TeamLayer.Enemy;

        return TeamLayer.None;
    }

    private TeamLayer ResolveOwnerTeam()
    {
        int rootLayer = transform.root.gameObject.layer;

        if (gameObject.layer != rootLayer)
            gameObject.layer = rootLayer;

        TeamLayer selfTeam = GetTeamFromLayer(gameObject.layer);
        if (selfTeam != TeamLayer.None)
            return selfTeam;

        return GetTeamFromLayer(rootLayer); // 루트(최상위 부모) 레이어 적용
    }

    public void GetDamage()
    {

    }

    public void SetWeaponCollider(int value)
    {
        m_collider.enabled = (value != 0);
        if (!m_collider.enabled)
            m_hitRoots.Clear();
    }
    public void SetWeaponCollider(bool enabled)
    {
        m_collider.enabled = enabled;
        if (!m_collider.enabled)
            m_hitRoots.Clear();
    }
    public void SetWeaponCollider(string command)
    {
        if (Global.OnCommand.Contains(command))
            m_collider.enabled = true;
        else
            m_collider.enabled = false;

        if (!m_collider.enabled)
            m_hitRoots.Clear();
    }
}
