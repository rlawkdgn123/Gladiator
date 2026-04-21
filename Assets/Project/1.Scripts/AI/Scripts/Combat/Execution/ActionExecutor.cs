using Game.Combat.Data;
using Game.Combat.State;
using Game.Core.Enums;

namespace Game.Combat.Execution
{
    public static class ActionExecutor
    {
        public static bool TryExecute(CombatState state, CombatAction action)
        {
            if (state == null || state.enemy == null)
                return false;

            switch (action)
            {
                case CombatAction.AttackTopHeavy:
                case CombatAction.AttackLeftHeavy:
                case CombatAction.AttackRightHeavy:
                    return TryStartAttack(state, action);

                case CombatAction.GuardTop:    return TryGuard(state, CombatAction.GuardTop,   AttackDirection.Top);
                case CombatAction.GuardLeft:   return TryGuard(state, CombatAction.GuardLeft,  AttackDirection.Left);
                case CombatAction.GuardRight:  return TryGuard(state, CombatAction.GuardRight, AttackDirection.Right);

                case CombatAction.Wait:
                    state.enemy.currentAction    = CombatAction.Wait;
                    state.enemy.currentPhase     = CombatPhase.Idle;
                    state.enemy.phaseElapsedMs   = 0f;
                    state.enemy.isGuarding       = false;
                    state.enemy.isParry          = false;
                    return true;

                default:
                    return false;
            }
        }

        // 방향만 다른 가드 공통 처리 — 공격속도/이동속도 조정 시에도 이 헬퍼만 수정
        static bool TryGuard(CombatState state, CombatAction action, AttackDirection direction)
        {
            state.enemy.currentAction    = action;
            state.enemy.currentDirection = direction;
            state.enemy.currentPhase     = CombatPhase.Idle;
            state.enemy.phaseElapsedMs   = 0f;
            state.enemy.isGuarding       = true;
            state.enemy.isParry          = false;
            return true;
        }

        static bool TryStartAttack(CombatState state, CombatAction action)
        {
            if (!AttackDatabase.TryGet(action, out var attackData))
                return false;

            state.enemy.currentAction = action;
            state.enemy.currentDirection = attackData.Direction;
            state.enemy.currentPhase = CombatPhase.Startup;
            state.enemy.phaseElapsedMs = 0f;
            state.enemy.isGuarding = false;
            state.enemy.isParry = false;
            state.enemy.hasResolvedThisAction = false;
            return true;
        }
    }
}