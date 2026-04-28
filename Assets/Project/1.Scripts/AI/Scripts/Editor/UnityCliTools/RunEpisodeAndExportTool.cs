using System;
using System.Collections.Generic;
using System.IO;
using Game.Combat.Execution;
using Game.Combat.Systems;
using Game.Core.Enums;
using Game.QA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "run_episode_and_export",
        Description = "Runs repeated decision ticks and exports the results",
        Group = "combat")]
    public static class RunEpisodeAndExportTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            int steps = 10;
            string format = "json";

            if (parameters != null)
            {
                if (parameters.TryGetValue("steps", out var stepToken))
                    steps = Mathf.Max(1, stepToken.Value<int>());

                if (parameters.TryGetValue("format", out var formatToken))
                    format = formatToken.Value<string>()?.ToLower() ?? "json";
            }

            var state = controller.State;

            var runLog = new EpisodeRunLog
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TotalSteps = steps,
                BrainType = state.currentBrain.ToString(),
                Personality = state.personality.ToString(),
                Difficulty = state.difficulty.ToString()
            };

            for (int i = 0; i < steps; i++)
            {
                CombatAction action = controller.TickDecisionAndGetAction();
                var reward = RewardCalculator.Calculate(state, action);

                runLog.Steps.Add(new EpisodeStepLog
                {
                    Step = i,
                    Action = action.ToString(),
                    EnemyDirection = state.enemy.currentDirection.ToString(),
                    EnemyPhase = state.enemy.currentPhase.ToString(),
                    PlayerHp = state.player.hp,
                    EnemyHp = state.enemy.hp,
                    BrainType = state.currentBrain.ToString(),
                    Personality = state.personality.ToString(),
                    Difficulty = state.difficulty.ToString(),
                    ValidHit = reward.ValidHit,
                    GuardPressure = reward.GuardPressure,
                    GuardBreakBonus = reward.GuardBreakBonus,
                    InjuryBonus = reward.InjuryBonus,
                    ShowmanshipBonus = reward.ShowmanshipBonus,
                    RepetitionPenalty = reward.RepetitionPenalty,
                    DistanceControlBonus = reward.DistanceControlBonus,
                    TotalReward = reward.Total
                });
            }

            string exportFolder = Path.Combine(Application.dataPath, "QA", "Logging", "Exports");
            Directory.CreateDirectory(exportFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath;

            if (format == "csv")
            {
                filePath = Path.Combine(exportFolder, $"episode_{timestamp}.csv");
                File.WriteAllText(filePath, BuildCsv(runLog));
            }
            else
            {
                filePath = Path.Combine(exportFolder, $"episode_{timestamp}.json");
                File.WriteAllText(filePath, JsonConvert.SerializeObject(runLog, Formatting.Indented));
            }

            return new SuccessResponse("Episode run complete.", new
            {
                steps = runLog.TotalSteps,
                format,
                filePath,
                brainType = runLog.BrainType,
                personality = runLog.Personality,
                difficulty = runLog.Difficulty
            });
        }

        static string BuildCsv(EpisodeRunLog runLog)
        {
            var lines = new List<string>
            {
                "step,action,enemyDirection,enemyPhase,playerHp,enemyHp,brainType,personality,difficulty,validHit,guardPressure,guardBreakBonus,injuryBonus,showmanshipBonus,repetitionPenalty,distanceControlBonus,totalReward"
            };

            foreach (var step in runLog.Steps)
            {
                lines.Add(
                    $"{step.Step}," +
                    $"{step.Action}," +
                    $"{step.EnemyDirection}," +
                    $"{step.EnemyPhase}," +
                    $"{step.PlayerHp}," +
                    $"{step.EnemyHp}," +
                    $"{step.BrainType}," +
                    $"{step.Personality}," +
                    $"{step.Difficulty}," +
                    $"{step.ValidHit}," +
                    $"{step.GuardPressure}," +
                    $"{step.GuardBreakBonus}," +
                    $"{step.InjuryBonus}," +
                    $"{step.ShowmanshipBonus}," +
                    $"{step.RepetitionPenalty}," +
                    $"{step.DistanceControlBonus}," +
                    $"{step.TotalReward}"
                );
            }

            return string.Join("\n", lines);
        }
    }
}