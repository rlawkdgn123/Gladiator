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

            if(fighter.currentAction == CombatAction.None)
                return;

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
                        fighter.hasResolvedThisAction = false;
                    }
                    break;
            }
         }
    }
}