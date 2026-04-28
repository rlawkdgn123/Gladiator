using Game.AI.Brains;
using Game.Combat.Execution;
using Game.Combat.Systems;
using Game.Core.Enums;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "reward_breakdown",
        Description = "Returns reward breakdown for the current or specified action",
        Group = "combat")]
    public static class RewardBreakdownTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var state = controller.State;
            var observation = ObservationBuilder.Build(state);

            CombatAction action;
            string actionSource;

            // action 파라미터가 있으면 그 행동 기준으로 계산
            if (parameters != null && parameters.TryGetValue("action", out var actionToken))
            {
                action = ParseAction(actionToken.Value<string>());

                if (action == CombatAction.None)
                    return new ErrorResponse("Invalid action name.");

                actionSource = "manual";
            }
            else
            {
                // 없으면 현재 Utility AI가 고를 행동 기준으로 계산
                var brain = new UtilityBrain(state.personality, state.difficulty);
                action = brain.Decide(in observation);
                actionSource = "utility";
            }

            var breakdown = RewardCalculator.Calculate(state, action);

            return new SuccessResponse("Reward breakdown calculated.", new
            {
                currentBrain = state.currentBrain.ToString(),
                personality = state.personality.ToString(),
                difficulty = state.difficulty.ToString(),
                action = action.ToString(),
                actionSource,
                reward = new
                {
                    validHit = breakdown.ValidHit,
                    guardPressure = breakdown.GuardPressure,
                    guardBreakBonus = breakdown.GuardBreakBonus,
                    injuryBonus = breakdown.InjuryBonus,
                    showmanshipBonus = breakdown.ShowmanshipBonus,
                    repetitionPenalty = breakdown.RepetitionPenalty,
                    distanceControlBonus = breakdown.DistanceControlBonus,
                    total = breakdown.Total
                }
            });
        }

        static CombatAction ParseAction(string value)
        {
            // ToLower = 소문자 변환. 만약 value가 null이면 null return. 
            // value != null ? value.ToLower() : null 이거랑 동일함. 
            return value?.ToLower() switch
            {
                "attacktopheavy" => CombatAction.AttackTopHeavy,
                "attackleftheavy" => CombatAction.AttackLeftHeavy,
                "attackrightheavy" => CombatAction.AttackRightHeavy,

                "guardtop" => CombatAction.GuardTop,
                "guardleft" => CombatAction.GuardLeft,
                "guardright" => CombatAction.GuardRight,

                "wait" => CombatAction.Wait,

                "top" => CombatAction.AttackTopHeavy,
                "left" => CombatAction.AttackLeftHeavy,
                "right" => CombatAction.AttackRightHeavy,

                _ => CombatAction.None
            };
        }
    }
}