using Game.Combat.State;
using Game.Core.Enums;
using Game.Core.Types;
using Game.QA.Logging;

namespace Game.Combat.Systems
{
    public static class CombatRuntimeResolver
    {
        public static DefenseResolveResult TryResolveEnemyAttackAgainstPlayer(CombatState state)
        {
            var result = new DefenseResolveResult();

            if (state == null || state.enemy == null || state.player == null)
                return result;

            if (state.enemy.currentAction == CombatAction.None)
                return result;

            if (state.enemy.currentPhase != CombatPhase.Active)
                return result;

            if (state.enemy.hasResolvedThisAction)
                return result;

            result = DefenseApplySystem.ApplyEnemyAttackAgainstPlayer(state);
            state.enemy.hasResolvedThisAction = true;
            CombatRuntimeLogger.Add(state, result);

            return result;
        }
    }
}


