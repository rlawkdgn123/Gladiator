using System.Linq;
using Game.QA.Logging;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "combat_runtime_log",
        Description = "Returns recent runtime combat resolve logs",
        Group = "combat")]
    public static class CombatRuntimeLogTool
    {
        public static object HandleCommand(JObject parameters)
        {
            int count = 10;

            if (parameters != null && parameters.TryGetValue("count", out var countToken))
                count = countToken.Value<int>();

            if (count < 1)
                count = 1;

            var entries = CombatRuntimeLogger.GetEntries()
                .TakeLast(count)
                .Select(x => new
                {
                    timestamp = x.Timestamp,
                    resultType = x.ResultType,
                    attackAction = x.AttackAction,
                    attackDirection = x.AttackDirection,
                    directionMatched = x.DirectionMatched,
                    wasParryWindow = x.WasParryWindow,
                    damage = x.Damage,
                    playerHp = x.PlayerHp,
                    enemyHp = x.EnemyHp,
                    playerDirection = x.PlayerDirection,
                    playerGuarding = x.PlayerGuarding,
                    playerParry = x.PlayerParry,
                    enemyDirection = x.EnemyDirection,
                    enemyPhase = x.EnemyPhase,
                    enemyAction = x.EnemyAction,
                    enemyActionElapsedMs = x.EnemyActionElapsedMs
                })
                .ToArray();

            return new SuccessResponse("Combat runtime log retrieved.", new
            {
                count = entries.Length,
                entries
            });
        }
    }
}