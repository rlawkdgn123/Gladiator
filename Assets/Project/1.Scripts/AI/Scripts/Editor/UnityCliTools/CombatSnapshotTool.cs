using Game.Combat.Execution;
using Game.Combat.State;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "combat_snapshot",
        Description = "Returns current combat state snapshot",
        Group = "combat")]
    public static class CombatSnapshotTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            CombatState state = controller.State;

            return new SuccessResponse("Combat snapshot", new
            {
                player = new
                {
                    hp = state.player.hp,
                    gh = new
                    {
                        top = state.player.guardHealth.top,
                        left = state.player.guardHealth.left,
                        right = state.player.guardHealth.right
                    },
                    injury = new
                    {
                        leftArm = state.player.injury.leftarm,
                        rightArm = state.player.injury.rightarm,
                        leftLeg = state.player.injury.leftleg,
                        rightLeg = state.player.injury.rightleg,
                        body = state.player.injury.body,
                        head = state.player.injury.head
                    },
                    hype = state.player.hype.current,
                    direction = state.player.currentDirection.ToString(),
                    phase = state.player.currentPhase.ToString(),
                    currentAction = state.player.currentAction.ToString(),
                    actionElapsedMs = state.player.actionElapsedMs,
                    phaseElapsedMs = state.player.phaseElapsedMs,
                    isGuarding = state.player.isGuarding,
                    isParry = state.player.isParry,
                    isParryWindowOpen = state.player.isParryWindowOpen,
                    hasResolvedThisAction = state.player.hasResolvedThisAction
                },
                enemy = new
                {
                    hp = state.enemy.hp,
                    gh = new
                    {
                        top = state.enemy.guardHealth.top,
                        left = state.enemy.guardHealth.left,
                        right = state.enemy.guardHealth.right
                    },
                    injury = new
                    {
                        leftArm = state.enemy.injury.leftarm,
                        rightArm = state.enemy.injury.rightarm,
                        leftLeg = state.enemy.injury.leftleg,
                        rightLeg = state.enemy.injury.rightleg,
                        body = state.enemy.injury.body,
                        head = state.enemy.injury.head
                    },
                    hype = state.enemy.hype.current,
                    direction = state.enemy.currentDirection.ToString(),
                    phase = state.enemy.currentPhase.ToString(),
                    currentAction = state.enemy.currentAction.ToString(),
                    actionElapsedMs = state.enemy.actionElapsedMs,
                    phaseElapsedMs = state.enemy.phaseElapsedMs,
                    isGuarding = state.enemy.isGuarding,
                    isParry = state.enemy.isParry,
                    isParryWindowOpen = state.enemy.isParryWindowOpen,
                    hasResolvedThisAction = state.enemy.hasResolvedThisAction
                },
                distanceBucket = state.distanceBucket,
                hasFrameAdvantage = state.hasFrameAdvantage,
                currentBrain = state.currentBrain.ToString(),
                personality = state.personality.ToString(),
                difficulty = state.difficulty.ToString(),
                enableAutoRuntimeResolve = state.enableAutoRuntimeResolve,
                useAnimationEventPhaseSync = state.useAnimationEventPhaseSync,
                recentAttacks = new
                {
                    a = state.recentAttackA.ToString(),
                    b = state.recentAttackB.ToString(),
                    c = state.recentAttackC.ToString()
                }
            });
        }
    }
}