using UnityEditor.Animations;
using UnityEngine;

public class EnemyAttackDummy : MonoBehaviour
{
    Animator m_anim;
    Weapon m_weapon;
    public bool AttackLoop = true;
    public float AttackDuration = 2.0f;
    public GuardZone GuardZone = GuardZone.Left;
    void Awake()
    {
        m_anim = GetComponent<Animator>();
        m_weapon = GetComponentInChildren<Weapon>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        if (!m_anim) return;

        m_anim.SetInteger("GuardZone", (int)GuardZone);

        if (AttackLoop)
        {
            Invoke("DoAttack", AttackDuration);
        }
    }

    void DoAttack()
    {
        m_anim.SetTrigger("DoAttack");
    }

    public void AnimEvent_SetWeaponCollider(int value)
    {
        if (!m_weapon)
            m_weapon = GetComponentInChildren<Weapon>();

        if (!m_weapon)
        {
            Debug.LogWarning("[EnemyAttackDummy] AnimEvent_SetWeaponCollider 호출 실패: Weapon을 찾지 못했습니다.", this);
            return;
        }

        m_weapon.SetWeaponCollider(value);
    }
}
