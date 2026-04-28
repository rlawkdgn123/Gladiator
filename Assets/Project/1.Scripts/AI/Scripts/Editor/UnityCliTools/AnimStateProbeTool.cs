using Game.Animation.Bridges;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "anim_state_probe",
        Description = "Returns current animator bridge state",
        Group = "combat")]
    public static class AnimStateProbeTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var bridge = Object.FindFirstObjectByType<EnemyAnimatorBridge>();

            if (bridge == null)
                return new ErrorResponse("EnemyAnimatorBridge not found in scene.");

            return new SuccessResponse("Animator state probed.", new
            {
                hasAnimator = bridge.HasAnimator(),
                inTransition = bridge.IsInTransition(),
                currentState = bridge.GetCurrentStateName(),
                normalizedTime = bridge.GetCurrentNormalizedTime(),
                actionType = bridge.GetActionTypeValue(),
                direction = bridge.GetDirectionValue(),
                phase = bridge.GetPhaseValue(),
                isParry = bridge.GetIsParryValue()
            });
        }
    }
}