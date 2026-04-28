using Game.AI.Brains;
using Game.Combat.Execution;
using Game.Combat.Systems;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "behavior_trace",
        Description = "Returns behavior tree trace and final action",
        Group = "combat")]
    public static class BehaviorTraceTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var state = controller.State;
            var observation = ObservationBuilder.Build(state);

            var brain = new BehaviorTreeBrain(state.personality, state.difficulty);
            var trace = brain.GetTrace(in observation);

            return new SuccessResponse("Behavior trace calculated.", new
            {
                currentBrain = state.currentBrain.ToString(),
                personality = state.personality.ToString(),
                difficulty = state.difficulty.ToString(),
                tacticalMode = trace.Mode.ToString(),
                action = trace.Action.ToString(),
                trace = trace.TraceLines
            });
        }
    }
}