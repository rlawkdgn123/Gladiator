using System.Collections.Generic;
using Game.Combat.Data;
using Game.Core.Enums;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "attack_data_probe",
        Description = "Returns registered attack frame and damage data",
        Group = "combat")]
    public static class AttackDataProbeTool
    {
        public static object HandleCommand(JObject parameters)
        {
            // 특정 액션 하나만 보고 싶을 때
            if (parameters != null &&
                parameters.TryGetValue("action", out var actionToken))
            {
                var action = ParseAction(actionToken.Value<string>());

                if (action == CombatAction.None)
                    return new ErrorResponse("Invalid action name.");

                if (!AttackDatabase.TryGet(action, out var singleData))
                    return new ErrorResponse("Attack data not found.");

                return new SuccessResponse("Attack data found.", ToPayload(singleData));
            }

            // 전체 등록 데이터 반환
            var actions = new List<CombatAction>
            {
                CombatAction.AttackTopHeavy,
                CombatAction.AttackLeftHeavy,
                CombatAction.AttackRightHeavy
            };

            var results = new List<object>();

            foreach (var action in actions)
            {
                if (AttackDatabase.TryGet(action, out var data))
                    results.Add(ToPayload(data));
            }

            return new SuccessResponse("Attack data list.", results.ToArray());
        }

        static object ToPayload(AttackData data)
        {
            return new
            {
                id = data.Id,
                action = data.Action.ToString(),
                direction = data.Direction.ToString(),
                startupMs = data.StartupMs,
                activeMs = data.ActiveMs,
                recoveryMs = data.RecoveryMs,
                damage = data.Damage
            };
        }

        static CombatAction ParseAction(string value)
        {
            return value?.ToLower() switch
            {
                "attacktopheavy" => CombatAction.AttackTopHeavy,
                "attackleftheavy" => CombatAction.AttackLeftHeavy,
                "attackrightheavy" => CombatAction.AttackRightHeavy,
                "top" => CombatAction.AttackTopHeavy,
                "left" => CombatAction.AttackLeftHeavy,
                "right" => CombatAction.AttackRightHeavy,
                _ => CombatAction.None
            };
        }
    }
}