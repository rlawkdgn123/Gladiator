using Game.Combat.Data;
using Game.Combat.State;
using Game.Core.Enums;


namespace Game.Combat.Systems
{
    public static class ActionPhaseDriver
    {
        public static void Tick(FighterState fighter, float deltaTime)
        {
            if(fighter == null)
                return;

            // Action=None인데 Phase가 Idle이 아닌 비정상 상태 → Idle로 강제 복귀.
            // (외부에서 action만 None으로 리셋되고 phase가 안 풀린 경우 데드락 방지)
            if (fighter.currentAction == CombatAction.None)
            {
                if (fighter.currentPhase != CombatPhase.Idle)
                {
                    fighter.currentPhase = CombatPhase.Idle;
                    fighter.currentDirection = AttackDirection.None;
                    fighter.phaseElapsedMs = 0f;
                    fighter.actionElapsedMs = 0f;
                    fighter.isParry = false;
                    fighter.isParryWindowOpen = false;
                    fighter.hasResolvedThisAction = false;
                }
                return;
            }

            if (!AttackDatabase.TryGet(fighter.currentAction, out var attackData))
                return;


            float deltaMs = deltaTime * 1000f;

            fighter.phaseElapsedMs += deltaMs;
            fighter.actionElapsedMs += deltaMs;

            switch (fighter.currentPhase)
            {
                case CombatPhase.Startup:
                    if(fighter.phaseElapsedMs >= attackData.StartupMs)
                    {
                        fighter.currentPhase = CombatPhase.Active;
                        fighter.phaseElapsedMs = 0f;
                    }
                    break;

                case CombatPhase.Active:
                    if (fighter.phaseElapsedMs >= attackData.ActiveMs)
                    {
                        fighter.currentPhase = CombatPhase.Recovery;
                        fighter.phaseElapsedMs = 0f;
                    }
                    break;

                case CombatPhase.Recovery:
                    if (fighter.phaseElapsedMs >= attackData.RecoveryMs)
                    {
                        fighter.currentPhase = CombatPhase.Idle;
                        fighter.currentAction = CombatAction.None;
                        fighter.currentDirection = AttackDirection.None;
                        fighter.phaseElapsedMs = 0f;
                        fighter.actionElapsedMs = 0f;
                        fighter.isParry = false;
                        fighter.isParryWindowOpen = false;
                        fighter.hasResolvedThisAction = false;
                    }
                    break;
            }
         }
    }
}
