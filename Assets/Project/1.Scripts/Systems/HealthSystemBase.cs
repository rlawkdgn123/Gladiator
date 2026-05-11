using System;
using UnityEditor.PackageManager;
using UnityEngine;
using VInspector;

public struct HitInfo
{
    public GameObject Attacker; // 공격자
    public GuardZone GuardZone; // 공격 방향
    public float Damage; // 데미지
    /*
     * public bool canGuard; // 가드 가능 여부
     * public bool canParry; // 가드 가능 여부
     * public float knockbackPower;
     * public AttackType attackType;
     * public float hitStunTime;
     */
}

public class HealthSystemBase : MonoBehaviour, IDamageable//, IKnockbackable
{
    [SerializeField] protected HealthSystemInfo.Components m_components = new();
    [SerializeField] protected HealthSystemInfo.CheckOption m_checkOption = new();
    [SerializeField] protected HealthSystemInfo.CurrentState m_currentState = new();
    [SerializeField] protected HealthSystemInfo.CurrentValue m_currentValue = new();
    [SerializeField] protected Status.Health m_health = new();

    protected HealthSystemInfo.Components Components => m_components;
    protected HealthSystemInfo.CheckOption CheckOptions => m_checkOption;
    protected HealthSystemInfo.CurrentState States => m_currentState;
    protected HealthSystemInfo.CurrentValue Values => m_currentValue;
    protected Status.Health Health => m_health;

    protected virtual void Awake()
    {
        Components.Animator = gameObject.GetComponent<Animator>();

        if (!Components.Animator)
            Debug.LogError("[HealthSystem] Animator 할당이 되지 않았습니다.");

        RefreshBodyHealth();
    }

    protected virtual void Start()
    {
        
    }

    protected virtual void Update()
    {
        // 가드존 갱신 코드를 넣을 것.
    }

    public virtual void ReceiveHit(HitInfo hitInfo)
    {
        bool mactchZone = (hitInfo.GuardZone == Values.GuardZone);

        if (!mactchZone)
        {
            Components.Animator.SetTrigger("DoHit");
            return;
        }
        else {
            Components.Animator.SetTrigger("DoGuard");
            // Parry도 추가 가능
        }   
    }

    public void TakeDamage(BodyParts.UnitBodyParts part, float damage)
    {
        if (Health == null)
            return;

        BodyParts.UnitBodyPart bodyParts = Health.GetBodyParts(part);
        if (bodyParts == null)
            return;

        bodyParts.CurrentHealth = Mathf.Max(0f, bodyParts.CurrentHealth - damage);

        if (bodyParts.MaxHealth > 0f)
            bodyParts.HealthPersent = (bodyParts.CurrentHealth / bodyParts.MaxHealth) * 100f;

        RefreshBodyHealth();
    }

    private void RefreshBodyHealth()
    {
        if (Health == null)
            return;

        float maxBodyHealth = 0f;
        float currentBodyHealth = 0f;

        foreach (BodyParts.UnitBodyParts part in Enum.GetValues(typeof(BodyParts.UnitBodyParts)))
        {
            BodyParts.UnitBodyPart bodyParts = Health.GetBodyParts(part);
            if (bodyParts == null)
                continue;

            maxBodyHealth += bodyParts.MaxHealth;
            currentBodyHealth += bodyParts.CurrentHealth;

            if (bodyParts.MaxHealth > 0f)
                bodyParts.HealthPersent = (bodyParts.CurrentHealth / bodyParts.MaxHealth) * 100f;
            else
                bodyParts.HealthPersent = 0f;
        }

        Health.MaxBodyHealth = maxBodyHealth;
        Health.CurrentBodyHealth = currentBodyHealth;

        if (maxBodyHealth > 0f)
            Health.BodyHealthPersent = (currentBodyHealth / maxBodyHealth) * 100f;
        else
            Health.BodyHealthPersent = 0f;
    }
}
