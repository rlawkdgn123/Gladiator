using System;
using UnityEngine;
using VInspector;




public class HealthSystem : MonoBehaviour, IDamageable//, IKnockbackable
{
    [SerializeField] private Status.Health m_health = new();
    [ReadOnly]
    [SerializeField] private Animator m_animator;

    private Status.Health Health => m_health;

    void Awake()
    {
        m_animator = gameObject.GetComponent<Animator>();

        if (!m_animator)
            Debug.LogError("[HealthSystem] Animator 할당이 되지 않았습니다.");

        RefreshBodyHealth();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
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
