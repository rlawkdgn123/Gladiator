using Game.Combat.Execution;
using Game.Combat.Systems;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "defense_resolve_probe",
        Description = "Resolves enemy attack against player and returns hit/guard/parry result",
        Group = "combat")]
    public static class DefenseResolveProbeTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var state = controller.State;
            var result = DefenseResolver.ResolveEnemyAttackAgainstPlayer(state);

            return new SuccessResponse("Defense resolve probed.", new
            {
                resultType = result.ResultType.ToString(),
                attackAction = result.AttackAction.ToString(),
                attackDirection = result.AttackDirection.ToString(),
                directionMatched = result.DirectionMatched,
                wasParryWindow = result.WasParryWindow,
                damage = result.Damage,
                player = new
                {
                    direction = state.player.currentDirection.ToString(),
                    isGuarding = state.player.isGuarding,
                    isParry = state.player.isParry
                },
                enemy = new
                {
                    currentAction = state.enemy.currentAction.ToString(),
                    direction = state.enemy.currentDirection.ToString(),
                    phase = state.enemy.currentPhase.ToString(),
                    actionElapsedMs = state.enemy.actionElapsedMs
                }
            });
        }
    }
}

