using Game.Core.Enums;
using UnityEngine;

namespace Game.Animation.Bridges
{
    public class EnemyAnimatorBridge : MonoBehaviour
    {
        [SerializeField] Animator animator;
        public Animator Animator => animator;

        static readonly int ActionTypeHash   = Animator.StringToHash("ActionType");
        static readonly int DirectionHash    = Animator.StringToHash("Direction");
        static readonly int PhaseHash        = Animator.StringToHash("Phase");
        static readonly int CommitActionHash = Animator.StringToHash("CommitAction");
        static readonly int IsParryHash      = Animator.StringToHash("IsParry");
        static readonly int IsMovingHash     = Animator.StringToHash("IsMove");
        static readonly int IsGuardWalkHash  = Animator.StringToHash("IsGuardWalk");
        static readonly int IsInCombatHash   = Animator.StringToHash("IsInCombat");

        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();          // 같은 오브젝트 먼저
            if (animator == null)
                animator = GetComponentInChildren<Animator>(); // 없으면 자식 탐색
            if (animator == null)
                animator = GetComponentInParent<Animator>();   // 그래도 없으면 부모 탐색

            if (animator == null)
                UnityEngine.Debug.LogError($"[EnemyAnimatorBridge] Animator를 찾을 수 없습니다: {gameObject.name}", this);
            else if (animator.applyRootMotion)
                animator.applyRootMotion = false;
        }

        // ActionType 상수 (컨트롤러와 동기화)
        const int AT_WAIT       = 0;
        const int AT_ATTACK     = 1;
        const int AT_GUARD      = 2;
        const int AT_HIT        = 3;
        const int AT_GUARDBREAK = 4;
        const int AT_STUNT      = 5;
        const int AT_DIE        = 6;

        // ── AI 결정 액션 ──────────────────────────────────
        public void ApplyAction(CombatAction action)
        {
            if (animator == null)
                return;

            // Guard 방향 변경은 Direction float만 갱신 — CommitAction 불필요 (블렌드 트리가 즉시 반영)
            bool isGuard = action == CombatAction.GuardTop
                        || action == CombatAction.GuardLeft
                        || action == CombatAction.GuardRight;

            animator.SetFloat(DirectionHash, ToDirection(action));

            if (!isGuard)
            {
                animator.SetInteger(ActionTypeHash, ToActionType(action));
                animator.SetTrigger(CommitActionHash);
            }
        }

        // ── 반응형 이벤트 (피격·사망 등 외부 트리거) ─────────
        static readonly int HitStateHash = Animator.StringToHash("Hit");

        public void ApplyHit(AttackDirection fromDirection)
        {
            if (animator == null) return;

            // Attack→Guard 같은 Has Exit Time transition이 진행 중이면 Any State→Hit 가
            // 묻혀버리고, 다음 결정 틱이 ActionType을 1로 덮어써서 Hit이 안 뜸.
            // → trigger 의존을 버리고 Play()로 강제 전이. transition 검사 자체를 우회.
            animator.ResetTrigger(CommitActionHash);
            animator.SetInteger(ActionTypeHash, AT_HIT);
            animator.SetFloat(DirectionHash, (float)fromDirection);
            animator.Play(HitStateHash, 0, 0f);
        }

        public void ApplyGuardBreak()
        {
            if (animator == null) return;
            animator.SetInteger(ActionTypeHash, AT_GUARDBREAK);
            animator.SetTrigger(CommitActionHash);
        }

        public void ApplyStunt()
        {
            if (animator == null) return;
            animator.SetInteger(ActionTypeHash, AT_STUNT);
            animator.SetTrigger(CommitActionHash);
        }

        public void ApplyDie()
        {
            if (animator == null) return;
            animator.SetInteger(ActionTypeHash, AT_DIE);
            animator.SetTrigger(CommitActionHash);
        }

        // ── 상태 보조 ─────────────────────────────────────
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

        public void ApplyGuardWalk(bool isGuardWalk)
        {
            if (animator == null)
                return;

            animator.SetBool(IsGuardWalkHash, isGuardWalk);
        }

        public void ApplyGuardWalkDirection(GuardWalkDirection direction)
        {
            if (animator == null)
                return;

            if (direction == GuardWalkDirection.None)
                return;

            animator.SetFloat(DirectionHash, (float)direction);
        }

        public void ApplyInCombat(bool isInCombat)
        {
            if (animator == null)
                return;

            animator.SetBool(IsInCombatHash, isInCombat);
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

        public float GetDirectionValue()
        {
            if (animator == null)
                return 0f;

            return animator.GetFloat(DirectionHash);
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

        public bool GetIsMovingValue()
        {
            if (animator == null) return false;
            return animator.GetBool(IsMovingHash);
        }

        public bool GetIsInCombatValue()
        {
            if (animator == null) return false;
            return animator.GetBool(IsInCombatHash);
        }

        public string GetCurrentStateFullName()
        {
            if (animator == null) return "NoAnimator";
            // 트랜지션 중이면 destination 상태명도 함께 반환
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                var curr = animator.GetCurrentAnimatorStateInfo(0);
                return $"{curr.shortNameHash} → {next.shortNameHash} (t={animator.GetAnimatorTransitionInfo(0).normalizedTime:F2})";
            }
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return $"hash={info.shortNameHash} norm={info.normalizedTime:F2}";
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

        float ToDirection(CombatAction action)
        {
            return action switch
            {
                CombatAction.AttackTopHeavy   => (float)AttackDirection.Top,
                CombatAction.AttackLeftHeavy  => (float)AttackDirection.Left,
                CombatAction.AttackRightHeavy => (float)AttackDirection.Right,
                CombatAction.GuardTop         => (float)AttackDirection.Top,
                CombatAction.GuardLeft        => (float)AttackDirection.Left,
                CombatAction.GuardRight       => (float)AttackDirection.Right,
                _                             => (float)AttackDirection.None
            };
        }
    }
}
