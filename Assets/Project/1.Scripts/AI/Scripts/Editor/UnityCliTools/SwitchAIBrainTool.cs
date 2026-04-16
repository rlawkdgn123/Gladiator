using Game.Combat.Execution;
using Game.Core.Enums;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "switch_ai_brain",
        Description = "Switches AI brain, personality, and difficulty",
        Group = "combat")]
    public static class SwitchAIBrainTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            if (parameters == null)
            {
                return new ErrorResponse("No parameters.");
            }

            if (!parameters.HasValues)
            {
                return new ErrorResponse("Has Values is empty.");
            }


            if (parameters.TryGetValue("brain", out var brainToken))
            {
                var brainType = ParseBrainType(brainToken.Value<string>());
                controller.SetBrain(brainType);
            }

            if (parameters.TryGetValue("personality", out var personalityToken))
            {
                var personality = ParsePersonalityType(personalityToken.Value<string>());
                controller.SetPersonality(personality);
            }

            if (parameters.TryGetValue("difficulty", out var difficultyToken))
            {
                var difficulty = ParseDifficultyType(difficultyToken.Value<string>());
                controller.SetDifficulty(difficulty);
            }

            return new SuccessResponse("AI settings updated.");
        }

        static BrainType ParseBrainType(string value)
        {
            return value?.ToLower() switch
            {
                "utility" => BrainType.Utility,
                "behaviortree" => BrainType.BehaviorTree,
                "ml" => BrainType.ML,
                "hybrid" => BrainType.Hybrid,
                _ => BrainType.None
            };
        }

        static AIPersonalityType ParsePersonalityType(string value)
        {
            return value?.ToLower() switch
            {
                "aggressive" => AIPersonalityType.Aggressive,
                "defensive" => AIPersonalityType.Defensive,
                _ => AIPersonalityType.Default
            };
        }

        static AIDifficultyType ParseDifficultyType(string value)
        {
            return value?.ToLower() switch
            {
                "easy" => AIDifficultyType.Easy,
                "hard" => AIDifficultyType.Hard,
                _ => AIDifficultyType.Normal
            };
        }
    }
}