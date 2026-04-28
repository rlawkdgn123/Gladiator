using System.Linq;
using Game.Combat.Execution;
using Game.QA.Profiling;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "profiling_probe",
        Description = "Profiles decision pipeline timings",
        Group = "combat")]
    public static class ProfilingProbeTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            int iterations = 1;
            bool includeSamples = false;

            if (parameters != null)
            {
                if (parameters.TryGetValue("iterations", out var iterationToken))
                    iterations = Mathf.Clamp(iterationToken.Value<int>(), 1, 100);

                if (parameters.TryGetValue("includeSamples", out var includeToken))
                    includeSamples = includeToken.Value<bool>();
            }

            var samples = new DecisionProfileResult[iterations];

            for (int i = 0; i < iterations; i++)
            {
                samples[i] = controller.ProfileCurrentDecision();
            }

            var avgObservation = samples.Average(x => x.ObservationMs);
            var avgDecision = samples.Average(x => x.DecisionMs);
            var avgExecute = samples.Average(x => x.ExecuteMs);
            var avgAnimator = samples.Average(x => x.AnimatorMs);
            var avgTotal = samples.Average(x => x.TotalMs);

            object samplePayload = null;

            if (includeSamples)
            {
                samplePayload = samples.Select(x => new
                {
                    brainType = x.BrainType,
                    personality = x.Personality,
                    difficulty = x.Difficulty,
                    action = x.Action,
                    enemyDirection = x.EnemyDirection,
                    enemyPhase = x.EnemyPhase,
                    executed = x.Executed,
                    animatorApplied = x.AnimatorApplied,
                    observationMs = x.ObservationMs,
                    decisionMs = x.DecisionMs,
                    executeMs = x.ExecuteMs,
                    animatorMs = x.AnimatorMs,
                    totalMs = x.TotalMs
                }).ToArray();
            }

            return new SuccessResponse("Profiling complete.", new
            {
                iterations,
                average = new
                {
                    observationMs = avgObservation,
                    decisionMs = avgDecision,
                    executeMs = avgExecute,
                    animatorMs = avgAnimator,
                    totalMs = avgTotal
                },
                samples = samplePayload
            });
        }
    }
}