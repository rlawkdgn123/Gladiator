using Game.Combat.Execution;
using UnityEngine;

namespace Game.Combat.HitDetection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        [SerializeField] EnemyCombatController owner;
        [SerializeField] bool setColliderAsTriggerOnReset = true;
        [SerializeField] LayerMask hitLayers = ~0;
        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField] int maxOverlapResults = 16;

        Collider hitboxCollider;
        Collider[] overlapResults;

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
            overlapResults = new Collider[Mathf.Max(1, maxOverlapResults)];
        }

        void OnValidate()
        {
            maxOverlapResults = Mathf.Max(1, maxOverlapResults);
        }

        void Update()
        {
            if (owner == null || !owner.CanDealWeaponHit())
                return;

            if (hitboxCollider == null)
                return;

            EnsureOverlapBuffer();

            int count = OverlapHitbox();
            for (int i = 0; i < count; i++)
            {
                if (!owner.CanDealWeaponHit())
                    break;

                TryResolve(overlapResults[i]);
            }
        }

        void EnsureOverlapBuffer()
        {
            if (overlapResults == null || overlapResults.Length != maxOverlapResults)
                overlapResults = new Collider[Mathf.Max(1, maxOverlapResults)];
        }

        int OverlapHitbox()
        {
            if (hitboxCollider is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                return Physics.OverlapBoxNonAlloc(center, halfExtents, overlapResults, box.transform.rotation, hitLayers, triggerInteraction);
            }

            if (hitboxCollider is SphereCollider sphere)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                float radius = sphere.radius * MaxAbsAxis(sphere.transform.lossyScale);
                return Physics.OverlapSphereNonAlloc(center, radius, overlapResults, hitLayers, triggerInteraction);
            }

            if (hitboxCollider is CapsuleCollider capsule)
            {
                GetCapsuleWorldPoints(capsule, out var pointA, out var pointB, out float radius);
                return Physics.OverlapCapsuleNonAlloc(pointA, pointB, radius, overlapResults, hitLayers, triggerInteraction);
            }

            Bounds bounds = hitboxCollider.bounds;
            return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, overlapResults, Quaternion.identity, hitLayers, triggerInteraction);
        }

        static float MaxAbsAxis(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            Transform t = capsule.transform;
            Vector3 center = t.TransformPoint(capsule.center);
            Vector3 scale = t.lossyScale;

            Vector3 axis;
            float heightScale;
            float radiusScale;
            switch (capsule.direction)
            {
                case 0:
                    axis = t.right;
                    heightScale = Mathf.Abs(scale.x);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    break;
                case 2:
                    axis = t.forward;
                    heightScale = Mathf.Abs(scale.z);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    break;
                default:
                    axis = t.up;
                    heightScale = Mathf.Abs(scale.y);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    break;
            }

            radius = capsule.radius * radiusScale;
            float halfHeight = Mathf.Max((capsule.height * heightScale * 0.5f) - radius, 0f);
            pointA = center + axis * halfHeight;
            pointB = center - axis * halfHeight;
        }

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
