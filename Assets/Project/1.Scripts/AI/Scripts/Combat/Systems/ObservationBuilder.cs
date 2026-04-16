using Game.Combat.AI;
using Game.Combat.State;
using Game.Core.Enums;
using Game.Core.Types;

namespace Game.Combat.Systems
{
    public static class ObservationBuilder
    {
        public static CombatObservation Build(CombatState state, AIParryLearner parryLearner = null)
        {
            return new CombatObservation
            {
                MyHp = state.enemy.hp,
                EnemyHp = state.player.hp,

                MyTopGH = state.enemy.guardHealth.top,
                MyLeftGH = state.enemy.guardHealth.left,
                MyRightGH = state.enemy.guardHealth.right,

                EnemyTopGH = state.player.guardHealth.top,
                EnemyLeftGH = state.player.guardHealth.left,
                EnemyRightGH = state.player.guardHealth.right,

                MyLeftArmInjured = state.enemy.injury.leftarm,
                MyRightArmInjured = state.enemy.injury.rightarm,
                MyLeftLegInjured = state.enemy.injury.leftleg,
                MyRightLegInjured = state.enemy.injury.rightleg,
                MyBodyInjured = state.enemy.injury.body,
                MyHeadInjured = state.enemy.injury.head,

                EnemyLeftArmInjured = state.player.injury.leftarm,
                EnemyRightArmInjured = state.player.injury.rightarm,
                EnemyLeftLegInjured = state.player.injury.leftleg,
                EnemyRightLegInjured = state.player.injury.rightleg,
                EnemyBodyInjured = state.player.injury.body,
                EnemyHeadInjured = state.player.injury.head,

                MyHype = state.enemy.hype.current,
                EnemyHype = state.player.hype.current,

                DistanceBucket = state.distanceBucket,
                HasFrameAdvantage = state.hasFrameAdvantage,

                RecentAttackA = state.recentAttackA,
                RecentAttackB = state.recentAttackB,
                RecentAttackC = state.recentAttackC,

                CurrentPhase = state.enemy.currentPhase,
                CurrentBrain = state.currentBrain,

                EnemyCurrentDirection = state.player.currentDirection,

                MyParryRateTop   = parryLearner?.GetParryRate(AttackDirection.Top)   ?? 0f,
                MyParryRateLeft  = parryLearner?.GetParryRate(AttackDirection.Left)  ?? 0f,
                MyParryRateRight = parryLearner?.GetParryRate(AttackDirection.Right) ?? 0f,

                MyIsGuarding = state.enemy.isGuarding,
                MyIsParry = state.enemy.isParry,
                EnemyIsGuarding = state.player.isGuarding,
                EnemyIsParry = state.player.isParry
            };
        }
    }
}