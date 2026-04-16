using Game.Core.Enums;
using UnityEngine;

namespace Game.Animation.Bridges
{
    public class EnemyAnimatorBridge : MonoBehaviour
    {
        [SerializeField] Animator animator;

        static readonly int ActionTypeHash = Animator.StringToHash("ActionType");
        static readonly int DirectionHash = Animator.StringToHash("Direction");
        static readonly int PhaseHash = Animator.StringToHash("Phase");
        static readonly int CommitActionHash = Animator.StringToHash("CommitAction");
        static readonly int IsParryHash = Animator.StringToHash("IsParry");
        static readonly int IsMovingHash = Animator.StringToHash("IsMove");

        public void ApplyAction(CombatAction action)
        {
            if (animator == null)
                return;

            animator.SetInteger(ActionTypeHash, ToActionType(action));
            animator.SetInteger(DirectionHash, ToDirection(action));
            animator.SetTrigger(CommitActionHash);
        }

        public void ApplyPhase(CombatPhase phase)
        {
            if (animator == null)
                return;

            animator.SetInteger(PhaseHash, (int)phase);
        }

        public void ApplyParry(bool isParry)
        {
            if (animator == null)
                return;

            animator.SetBool(IsParryHash, isParry);
        }

        public void ApplyMoving(bool isMoving)
        {
            if (animator == null)
                return;

            animator.SetBool(IsMovingHash, isMoving);
        }

        public bool HasAnimator()
        {
            return animator != null;
        }

        public bool IsInTransition()
        {
            if (animator == null)
                return false;

            return animator.IsInTransition(0);
        }

        public string GetCurrentStateName()
        {
            if (animator == null)
                return "NoAnimator";

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.shortNameHash.ToString();
        }

        public float GetCurrentNormalizedTime()
        {
            if (animator == null)
                return 0f;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.normalizedTime;
        }

        public int GetActionTypeValue()
        {
            if (animator == null)
                return 0;

            return animator.GetInteger(ActionTypeHash);
        }

        public int GetDirectionValue()
        {
            if (animator == null)
                return 0;

            return animator.GetInteger(DirectionHash);
        }

        public int GetPhaseValue()
        {
            if (animator == null)
                return 0;

            return animator.GetInteger(PhaseHash);
        }

        public bool GetIsParryValue()
        {
            if (animator == null)
                return false;

            return animator.GetBool(IsParryHash);
        }

        int ToActionType(CombatAction action)
        {
            return action switch
            {
                CombatAction.AttackTopHeavy => 1,
                CombatAction.AttackLeftHeavy => 1,
                CombatAction.AttackRightHeavy => 1,
                CombatAction.GuardTop => 2,
                CombatAction.GuardLeft => 2,
                CombatAction.GuardRight => 2,
                CombatAction.Wait => 0,
                _ => 0
            };
        }

        int ToDirection(CombatAction action)
        {
            return action switch
            {
                CombatAction.AttackTopHeavy => (int)AttackDirection.Top,
                CombatAction.AttackLeftHeavy => (int)AttackDirection.Left,
                CombatAction.AttackRightHeavy => (int)AttackDirection.Right,
                CombatAction.GuardTop => (int)AttackDirection.Top,
                CombatAction.GuardLeft => (int)AttackDirection.Left,
                CombatAction.GuardRight => (int)AttackDirection.Right,
                _ => (int)AttackDirection.None
            };
        }
    }
}