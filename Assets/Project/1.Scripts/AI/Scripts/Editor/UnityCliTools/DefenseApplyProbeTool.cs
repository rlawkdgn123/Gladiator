using Game.Combat.Execution;
using Game.Combat.Systems;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "defense_apply_probe",
        Description = "Applies defense resolve result to HP/GH and returns the updated state",
        Group = "combat")]
    public static class DefenseApplyProbeTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            var state = controller.State;
            var result = DefenseApplySystem.ApplyEnemyAttackAgainstPlayer(state);

            return new SuccessResponse("Defense apply probed.", new
            {
                resultType = result.ResultType.ToString(),
                attackAction = result.AttackAction.ToString(),
                attackDirection = result.AttackDirection.ToString(),
                directionMatched = result.DirectionMatched,
                wasParryWindow = result.WasParryWindow,
                damage = result.Damage,
                player = new
                {
                    hp = state.player.hp,
                    gh = new
                    {
                        top = state.player.guardHealth.top,
                        left = state.player.guardHealth.left,
                        right = state.player.guardHealth.right
                    },
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