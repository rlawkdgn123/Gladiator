using Game.Combat.Data;
using Game.Combat.State;
using Game.Core.Enums;
using System.Diagnostics;

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

                case CombatAction.ParryTop:    return TryParry(state, CombatAction.ParryTop,   AttackDirection.Top);
                case CombatAction.ParryLeft:   return TryParry(state, CombatAction.ParryLeft,  AttackDirection.Left);
                case CombatAction.ParryRight:  return TryParry(state, CombatAction.ParryRight, AttackDirection.Right);

                case CombatAction.Wait:
                    state.enemy.currentAction    = CombatAction.Wait;
                    state.enemy.currentPhase     = CombatPhase.Idle;
                    state.enemy.currentDirection = AttackDirection.None;
                    state.enemy.phaseElapsedMs   = 0f;
                    state.enemy.actionElapsedMs  = 0f;
                    state.enemy.isGuarding       = false;
                    state.enemy.isParry          = false;
                    state.enemy.isParryWindowOpen = false;
                    return true;

                default:
                    return false;
            }
        }

        // 방향만 다른 가드 공통 처리 — 공격속도/이동속도 조정 시 여기서 조절.
        static bool TryGuard(CombatState state, CombatAction action, AttackDirection direction)
        {
            state.enemy.currentAction    = action;
            state.enemy.currentDirection = direction;
            state.enemy.currentPhase     = CombatPhase.Idle;
            state.enemy.phaseElapsedMs   = 0f;
            state.enemy.actionElapsedMs  = 0f;
            state.enemy.isGuarding       = true;
            state.enemy.isParry          = false;
            state.enemy.isParryWindowOpen = false;
            return true;
        }

        static bool TryParry(CombatState state, CombatAction action, AttackDirection direction)
        {
            state.enemy.currentAction        = action;
            state.enemy.currentDirection     = direction;
            state.enemy.currentPhase         = CombatPhase.Idle;
            state.enemy.phaseElapsedMs       = 0f;
            state.enemy.actionElapsedMs      = 0f;
            state.enemy.isGuarding           = true;
            state.enemy.isParry              = true;
            state.enemy.isParryWindowOpen    = true;
            return true;
        }

        static bool TryStartAttack(CombatState state, CombatAction action)
        {
            if (!AttackDatabase.TryGet(action, out var attackData))
            {
                Debug.WriteLine("Attack Try Get 실패.");
                return false;
            }
               

            state.enemy.currentAction = action;
            state.enemy.currentDirection = attackData.Direction;
            state.enemy.currentPhase = CombatPhase.Startup;
            state.enemy.phaseElapsedMs = 0f;
            state.enemy.actionElapsedMs = 0f;
            state.enemy.isGuarding = false;
            state.enemy.isParry = false;
            state.enemy.isParryWindowOpen = false;
            state.enemy.hasResolvedThisAction = false;
            return true;
        }
    }
}
