using BodyParts;
using UnityEngine;

public interface IDamageable
{
    public void TakeDamage(BodyParts.UnitBodyParts part, float damage);
}
public interface IParryable
{
    public void Parry();
}
public interface IKnockbackable
{
    public void ApplyKnockback(Vector3 direction, float force);
}
