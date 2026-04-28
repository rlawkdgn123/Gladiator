using Game.Combat.Execution;
using Game.Core.Enums;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "parry_learner_probe",
        Description = "Inspects AI parry learner state and simulates player attack recording",
        Group = "combat")]
    public static class ParryLearnerProbeTool
    {
        // Actions:
        //   (no params)         → 현재 learner 상태 스냅샷 출력
        //   --record top/left/right → 해당 방향 플레이어 공격 기록
        //   --reset             → 학습 데이터 초기화

        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var learner = controller.ParryLearner;

            // --reset
            if (parameters != null && parameters.TryGetValue("reset", out var resetToken) && resetToken.Value<bool>())
            {
                learner.Reset();
                return new SuccessResponse("Parry learner reset.");
            }

            // --record <direction>
            if (parameters != null && parameters.TryGetValue("record", out var recordToken))
            {
                var dir = ParseDirection(recordToken.Value<string>());
                if (dir == AttackDirection.None)
                    return new ErrorResponse($"Unknown direction: {recordToken.Value<string>()}. Use top/left/right.");

                learner.RecordPlayerAttack(dir);
                var snapshot = learner.GetDebugSnapshot();
                return new SuccessResponse($"Recorded {dir}", snapshot);
            }

            // 기본: 현재 상태 스냅샷
            return new SuccessResponse("Parry learner state", learner.GetDebugSnapshot());
        }

        static AttackDirection ParseDirection(string value) => value?.ToLower() switch
        {
            "top"   => AttackDirection.Top,
            "left"  => AttackDirection.Left,
            "right" => AttackDirection.Right,
            _ => AttackDirection.None
        };
    }
}
