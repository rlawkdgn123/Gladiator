using Game.Combat.Data;
using Game.Combat.State;
using Game.Core.Enums;
using Game.Core.Types;

namespace Game.Combat.Systems
{
    public static class DefenseResolver
    {
        public static DefenseResolveResult ResolveEnemyAttackAgainstPlayer(CombatState state)
        {
            var result = new DefenseResolveResult();

            if (state == null || state.enemy == null || state.player == null)
                return result;

            var attacker = state.enemy;
            var defender = state.player;

            if (attacker.currentPhase != CombatPhase.Active)
            {
                result.ResultType = DefenseResultType.Miss;
                return result;
            }

            if (!AttackDatabase.TryGet(attacker.currentAction, out var attackData))
            {
                result.ResultType = DefenseResultType.Miss;
                return result;
            }

            bool directionMatched = defender.currentDirection == attackData.Direction;
            bool attackIsParryable =
                attacker.actionElapsedMs >= attackData.ParryWindowStartMs &&
                attacker.actionElapsedMs <= attackData.ParryWindowEndMs;
            bool defenderParryWindow = defender.isParryWindowOpen || defender.isParry;

            result.AttackAction = attacker.currentAction;
            result.AttackDirection = attackData.Direction;
            result.DirectionMatched = directionMatched;
            result.WasParryWindow = attackIsParryable && defenderParryWindow;
            result.Damage = attackData.Damage;

            if (defender.isParry && directionMatched && attackIsParryable && defenderParryWindow)
            {
                result.ResultType = DefenseResultType.Parry;
                result.Damage = 0;
                return result;
            }

            if (defender.isGuarding && directionMatched)
            {
                result.ResultType = DefenseResultType.Guard;
                result.Damage = 0;
                return result;
            }

            result.ResultType = DefenseResultType.Hit;
            return result;
        }
    }
}
