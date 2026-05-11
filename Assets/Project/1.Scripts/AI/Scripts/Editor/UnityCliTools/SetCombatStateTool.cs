using Game.Combat.Execution;
using Game.Combat.State;
using Game.Core.Enums;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "set_combat_state",
        Description = "Sets combat state values for testing. Use player_/enemy_ prefix for fighter fields.",
        Group = "combat")]
    public static class SetCombatStateTool
    {
        public static object HandleCommand(JObject parameters)
        {
            var controller = Object.FindFirstObjectByType<EnemyCombatController>();

            if (controller == null)
                return new ErrorResponse("EnemyCombatController not found in scene.");

            CombatState state = controller.State;

            ApplyFighterFlat(state.player, parameters, "player");
            ApplyFighterFlat(state.enemy, parameters, "enemy");

            if (parameters.TryGetValue("distanceBucket", out var distanceToken))
                state.distanceBucket = distanceToken.Value<int>();

            if (parameters.TryGetValue("hasFrameAdvantage", out var frameToken))
                state.hasFrameAdvantage = frameToken.Value<bool>();

            if (parameters.TryGetValue("recentAttackA", out var recentAToken))
                state.recentAttackA = ParseDirection(recentAToken.Value<string>());

            if (parameters.TryGetValue("recentAttackB", out var recentBToken))
                state.recentAttackB = ParseDirection(recentBToken.Value<string>());

            if (parameters.TryGetValue("recentAttackC", out var recentCToken))
                state.recentAttackC = ParseDirection(recentCToken.Value<string>());

            if (parameters.TryGetValue("enableAutoRuntimeResolve", out var autoResolveToken))
                state.enableAutoRuntimeResolve = autoResolveToken.Value<bool>();

            return new SuccessResponse("Combat state updated.");
        }

        static void ApplyFighterFlat(FighterState fighter, JObject p, string prefix)
        {
            if (p.TryGetValue($"{prefix}_hp", out var hp))
                fighter.hp = hp.Value<float>();

            if (p.TryGetValue($"{prefix}_action", out var action))
                fighter.currentAction = ParseAction(action.Value<string>());

            if (p.TryGetValue($"{prefix}_direction", out var direction))
                fighter.currentDirection = ParseDirection(direction.Value<string>());

            if (p.TryGetValue($"{prefix}_phase", out var phase))
                fighter.currentPhase = ParsePhase(phase.Value<string>());

            if (p.TryGetValue($"{prefix}_elapsed", out var elapsed))
                fighter.actionElapsedMs = elapsed.Value<float>();

            if (p.TryGetValue($"{prefix}_phase_elapsed", out var phaseElapsed))
                fighter.phaseElapsedMs = phaseElapsed.Value<float>();

            if (p.TryGetValue($"{prefix}_guarding", out var guarding))
                fighter.isGuarding = guarding.Value<bool>();

            if (p.TryGetValue($"{prefix}_parry", out var parry))
                fighter.isParry = parry.Value<bool>();

            if (p.TryGetValue($"{prefix}_hype", out var hype))
                fighter.hype.current = hype.Value<float>();

            if (p.TryGetValue($"{prefix}_gh_top", out var ghTop))
                fighter.guardHealth.top = ghTop.Value<float>();

            if (p.TryGetValue($"{prefix}_gh_left", out var ghLeft))
                fighter.guardHealth.left = ghLeft.Value<float>();

            if (p.TryGetValue($"{prefix}_gh_right", out var ghRight))
                fighter.guardHealth.right = ghRight.Value<float>();

            if (p.TryGetValue($"{prefix}_injury_leftarm", out var leftArm))
                fighter.injury.leftArm = leftArm.Value<bool>();

            if (p.TryGetValue($"{prefix}_injury_rightarm", out var rightArm))
                fighter.injury.rightArm = rightArm.Value<bool>();

            if (p.TryGetValue($"{prefix}_injury_leftleg", out var leftLeg))
                fighter.injury.leftLeg = leftLeg.Value<bool>();

            if (p.TryGetValue($"{prefix}_injury_rightleg", out var rightLeg))
                fighter.injury.rightLeg = rightLeg.Value<bool>();

            if (p.TryGetValue($"{prefix}_injury_body", out var body))
                fighter.injury.body = body.Value<bool>();

            if (p.TryGetValue($"{prefix}_injury_head", out var head))
                fighter.injury.head = head.Value<bool>();
        }

        static AttackDirection ParseDirection(string value)
        {
            return value?.ToLower() switch
            {
                "top" => AttackDirection.Top,
                "left" => AttackDirection.Left,
                "right" => AttackDirection.Right,
                _ => AttackDirection.None
            };
        }

        static CombatPhase ParsePhase(string value)
        {
            return value?.ToLower() switch
            {
                "startup" => CombatPhase.Startup,
                "active" => CombatPhase.Active,
                "recovery" => CombatPhase.Recovery,
                _ => CombatPhase.Idle
            };
        }

        static CombatAction ParseAction(string value)
        {
            return value?.ToLower() switch
            {
                "attacktopheavy" => CombatAction.AttackTopHeavy,
                "attackleftheavy" => CombatAction.AttackLeftHeavy,
                "attackrightheavy" => CombatAction.AttackRightHeavy,
                "guardtop" => CombatAction.GuardTop,
                "guardleft" => CombatAction.GuardLeft,
                "guardright" => CombatAction.GuardRight,
                "parrytop" => CombatAction.ParryTop,
                "parryleft" => CombatAction.ParryLeft,
                "parryright" => CombatAction.ParryRight,
                "wait" => CombatAction.Wait,
                "top" => CombatAction.AttackTopHeavy,
                "left" => CombatAction.AttackLeftHeavy,
                "right" => CombatAction.AttackRightHeavy,
                _ => CombatAction.None
            };
        }
    }
}
