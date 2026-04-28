using Game.Combat.Execution;
using Game.Core.Enums;
using UnityEngine;

namespace Game.Animation.Bridges
{
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField] EnemyCombatController controller;

        bool IsReady()
        {
            if(controller == null)
                Debug.Log("AnimationEventRelay에 combatcontroller 없음");

            return controller != null && controller.State != null && controller.State.enemy != null;
        }

        public void OnAttackStartup()
        {
            if (!IsReady())
                return;

            controller.State.enemy.currentPhase = CombatPhase.Startup;
            controller.State.enemy.phaseElapsedMs = 0f;
        }

        public void OnAttackActiveStart()
        {
            if (!IsReady())
                return;

            controller.State.enemy.currentPhase = CombatPhase.Active;
            controller.State.enemy.phaseElapsedMs = 0f;
        }

        public void OnAttackRecoveryStart()
        {
            if (!IsReady())
                return;

            controller.State.enemy.currentPhase = CombatPhase.Recovery;
            controller.State.enemy.phaseElapsedMs = 0f;
        }

        public void OnAttackFinished()
        {
            if (!IsReady())
                return;

            controller.State.enemy.currentPhase = CombatPhase.Idle;
            controller.State.enemy.currentAction = CombatAction.None;
            controller.State.enemy.currentDirection = AttackDirection.None;
            controller.State.enemy.phaseElapsedMs = 0f;
            controller.State.enemy.actionElapsedMs = 0f;
            controller.State.enemy.isParry = false;
            controller.State.enemy.isParryWindowOpen = false;
            controller.State.enemy.hasResolvedThisAction = false;
        }

        public void OnParryWindowStart()
        {
            if (!IsReady())
                return;

            controller.State.enemy.isParryWindowOpen = true;
        }

        public void OnParryWindowEnd()
        {
            if (!IsReady())
                return;

            controller.State.enemy.isParryWindowOpen = false;
        }
    }
}