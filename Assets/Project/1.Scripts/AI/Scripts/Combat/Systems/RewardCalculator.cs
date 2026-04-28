using Game.Combat.State;
using Game.Core.Enums;
using Game.Core.Types;

namespace Game.Combat.Systems
{
    // 보상 계산용. 추후에 변경 가능.
    public static class RewardCalculator
    {
        public static RewardBreakdownData Calculate(CombatState state, CombatAction chosenAction)
        {
            var result = new RewardBreakdownData();

            if (chosenAction == CombatAction.AttackTopHeavy ||
                chosenAction == CombatAction.AttackLeftHeavy ||
                chosenAction == CombatAction.AttackRightHeavy)
            {
                result.GuardPressure += CalculateWeakGuardPressure(state, chosenAction);
                result.ShowmanshipBonus += CalculateShowmanshipBonus(state, chosenAction);
            }

            if (state.hasFrameAdvantage)
            {
                result.ValidHit += 0.5f;
            }

            if (state.enemy.hype.IsHigh)
            {
                result.GuardPressure += 0.5f;
            }

            if (state.enemy.hype.IsLow)
            {
                result.DistanceControlBonus += 0.5f;
            }

            result.InjuryBonus += CalculateInjuryBonus(state, chosenAction);

            result.Total =
                result.ValidHit +
                result.GuardPressure +
                result.GuardBreakBonus +
                result.InjuryBonus +
                result.ShowmanshipBonus +
                result.DistanceControlBonus -
                result.RepetitionPenalty;

            return result;
        }

        private static float CalculateWeakGuardPressure(CombatState state, CombatAction action)
        {
            return action switch
            {
                CombatAction.AttackTopHeavy when state.player.guardHealth.GetTopTier() == GuardTier.Critical => 1.0f,
                CombatAction.AttackLeftHeavy when state.player.guardHealth.GetLeftTier() == GuardTier.Critical => 1.0f,
                CombatAction.AttackRightHeavy when state.player.guardHealth.GetRightTier() == GuardTier.Critical => 1.0f,
                _ => 0f
            };
        }

        private static float CalculateInjuryBonus(CombatState state, CombatAction action)
        {
            // 플레이어 부상 부위에 맞는 방향 공격 시 보너스
            // Top → 머리, Left → 오른팔(상대 기준), Right → 왼팔(상대 기준)
            // Body 부상 시 모든 공격에 소량 보너스
            float bonus = 0f;

            bool isDirectHit = action switch
            {
                CombatAction.AttackTopHeavy   => state.player.injury.head,
                CombatAction.AttackLeftHeavy  => state.player.injury.rightArm,
                CombatAction.AttackRightHeavy => state.player.injury.leftArm,
                _                             => false
            };

            if (isDirectHit) bonus += 1.0f;
            if (state.player.injury.body) bonus += 0.3f;

            return bonus;
        }

        private static float CalculateShowmanshipBonus(CombatState state, CombatAction action)
        {
            var direction = action switch
            {
                CombatAction.AttackTopHeavy => AttackDirection.Top,
                CombatAction.AttackLeftHeavy => AttackDirection.Left,
                CombatAction.AttackRightHeavy => AttackDirection.Right,
                _ => AttackDirection.None
            };

            if (direction == AttackDirection.None)
                return 0f;

            if (direction != state.recentAttackA &&
                direction != state.recentAttackB &&
                direction != state.recentAttackC)
            {
                return 0.5f;
            }

            return 0f;
        }
    }
}