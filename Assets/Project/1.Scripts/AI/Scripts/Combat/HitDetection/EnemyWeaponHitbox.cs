using Game.Combat.Execution;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Combat.HitDetection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        [SerializeField] EnemyCombatController owner;
        [SerializeField] bool setColliderAsTriggerOnReset = true;

        Collider hitboxCollider;

        void Reset()
        {
            owner = GetComponentInParent<EnemyCombatController>();

            var colliderComponent = GetComponent<Collider>();
            if (setColliderAsTriggerOnReset && colliderComponent != null)
                colliderComponent.isTrigger = true;
        }

        void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<EnemyCombatController>();

            hitboxCollider = GetComponent<Collider>();
        }

        void OnTriggerEnter(Collider other) => TryResolve(other);
        void OnTriggerStay(Collider other) => TryResolve(other);

        void TryResolve(Collider other)
        {
            if (other == null || other == hitboxCollider)
                return;

            // 상대 칼 / 자기 칼 / 지면(Terrain) 등은 즉시 무시 (히트 후보 아님)
            if (other.GetComponent<EnemyWeaponHitbox>() != null) return;
            if (other.GetComponent<Terrain>() != null) return;
            if (other.GetComponent<TerrainCollider>() != null) return;

            // OnTriggerStay 가 매 프레임 발동하므로, 노이즈 줄이기 위해
            // CanDealWeaponHit 통과 못하면 조용히 리턴 (가드 fail 로그 생략).
            if (owner == null || !owner.CanDealWeaponHit())
                return;

            bool log = owner.verboseHitLogging;

            if (other == null || other == hitboxCollider)
                return;

            var hurtbox = other.GetComponentInParent<EnemyHurtbox>();
            if (hurtbox == null)
            {
                if (log) UnityEngine.Debug.Log($"[Hitbox] {owner.name} → no Hurtbox on '{other.name}' (parent chain). Add EnemyHurtbox to body collider GO or its parent.", this);
                return;
            }
            if (hurtbox.Owner == null)
            {
                if (log) UnityEngine.Debug.LogWarning($"[Hitbox] Hurtbox on '{other.name}' has Owner=null", this);
                return;
            }
            if (hurtbox.Owner == owner)
            {
                if (log) UnityEngine.Debug.Log($"[Hitbox] Self-hit ignored on {owner.name}", this);
                return;
            }

            if (log) UnityEngine.Debug.Log($"[Hitbox] {owner.name} → HIT {hurtbox.Owner.name} (phase={owner.State.enemy.currentPhase} action={owner.State.enemy.currentAction})", this);
            owner.TryResolveWeaponHit(hurtbox.Owner);
        }
    }
}
