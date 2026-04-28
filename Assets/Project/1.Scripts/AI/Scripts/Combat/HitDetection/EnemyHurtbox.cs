using Game.Combat.Execution;
using UnityEngine;

namespace Game.Combat.HitDetection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EnemyHurtbox : MonoBehaviour
    {
        [SerializeField] EnemyCombatController owner;

        public EnemyCombatController Owner => owner;

        void Reset()
        {
            owner = GetComponentInParent<EnemyCombatController>();
        }

        void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<EnemyCombatController>();
        }
    }
}
