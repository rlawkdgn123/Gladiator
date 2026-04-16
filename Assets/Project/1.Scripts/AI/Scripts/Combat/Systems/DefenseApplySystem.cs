using Game.Combat.State;
using Game.Combat.Systems;
using Game.Core.Enums;
using Game.Core.Types;


namespace Game.Combat.Systems
{
    public static class DefenseApplySystem
    {
        public static DefenseResolveResult ApplyEnemyAttackAgainstPlayer(CombatState state)
        {
            var result = DefenseResolver.ResolveEnemyAttackAgainstPlayer(state);

            if (state == null || state.player == null || state.enemy == null)
                return result;

            switch (result.ResultType)
            {
                case DefenseResultType.Hit:
                    state.player.hp -= result.Damage;

                    if (state.player.hp < 0)
                        state.player.hp = 0;
                    break;
                case DefenseResultType.Guard:
                    ApplyGuardPressure(state, result.AttackDirection, result.Damage);
                    break;
                case DefenseResultType.Parry:
                    state.enemy.currentPhase = CombatPhase.Recovery;
                    state.enemy.phaseElapsedMs = 0f;
                    state.enemy.isParry = false;
                    break;
            }

            state.player.isParry = false;
            return result;
        }

        static void ApplyGuardPressure(CombatState state, AttackDirection direction, int damage)
        {
            float pressure = damage;

            switch (direction)
            {
                case AttackDirection.Top:
                    state.player.guardHealth.top -= pressure;
                    if (state.player.guardHealth.top < 0f)
                        state.player.guardHealth.top = 0f;
                    break;

                case AttackDirection.Left:
                    state.player.guardHealth.left -= pressure;
                    if (state.player.guardHealth.left < 0f)
                        state.player.guardHealth.left = 0f;
                    break;

                case AttackDirection.Right:
                    state.player.guardHealth.right -= pressure;
                    if (state.player.guardHealth.right < 0f)
                        state.player.guardHealth.right = 0f;
                    break;
            }
        }

    }
}




