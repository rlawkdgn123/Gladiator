using System.Linq;
using Game.AI.Brains;
using Game.Combat.Execution;
using Game.Combat.Systems;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "action_candidates",
        Description = "Returns current Utility AI action scores",
        Group = "combat")]
    public static class ActionCandidatesTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var state = controller.State;
            var observation = ObservationBuilder.Build(state);

            var brain = new UtilityBrain(state.personality, state.difficulty);
            var candidates = brain.GetCandidates(in observation);

            var ordered = candidates
                .OrderByDescending(c => c.Score)
                .Select(c => new
                {
                    action = c.Action.ToString(),
                    score = c.Score
                })
                .ToArray();

            return new SuccessResponse("Action candidates calculated.", new
            {
                currentBrain = state.currentBrain.ToString(),
                personality = state.personality.ToString(),
                difficulty = state.difficulty.ToString(),
                candidates = ordered
            });
        }
    }
}