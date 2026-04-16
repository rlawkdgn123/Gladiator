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

                case CombatAction.GuardTop:
                    state.enemy.currentAction = CombatAction.GuardTop;
                    state.enemy.currentDirection = AttackDirection.Top;
                    state.enemy.currentPhase = CombatPhase.Idle;
                    state.enemy.phaseElapsedMs = 0f;
                    state.enemy.isGuarding = true;
                    state.enemy.isParry = false;
                    return true;

                case CombatAction.GuardLeft:
                    state.enemy.currentAction = CombatAction.GuardLeft;
                    state.enemy.currentDirection = AttackDirection.Left;
                    state.enemy.currentPhase = CombatPhase.Idle;
                    state.enemy.phaseElapsedMs = 0f;
                    state.enemy.isGuarding = true;
                    state.enemy.isParry = false;
                    return true;

                case CombatAction.GuardRight:
                    state.enemy.currentAction = CombatAction.GuardRight;
                    state.enemy.currentDirection = AttackDirection.Right;
                    state.enemy.currentPhase = CombatPhase.Idle;
                    state.enemy.phaseElapsedMs = 0f;
                    state.enemy.isGuarding = true;
                    state.enemy.isParry = false;
                    return true;

                case CombatAction.Wait:
                    state.enemy.currentAction = CombatAction.Wait;
                    state.enemy.currentPhase = CombatPhase.Idle;
                    state.enemy.phaseElapsedMs = 0f;
                    state.enemy.isGuarding = false;
                    state.enemy.isParry = false;
                    return true;

                default:
                    return false;
            }
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