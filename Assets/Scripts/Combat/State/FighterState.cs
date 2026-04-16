using Game.Core.Enums;

namespace Game.Combat.State
{
    [System.Serializable]
    public class FighterState
    {
        public string name;
        public float hp = 100f;
        public GuardHealthState guardHealth = new();
        public InjuryState injury = new();
        public HypeState hype = new();

        public AttackDirection currentDirection = AttackDirection.None;
        public CombatPhase currentPhase = CombatPhase.Idle;

        public CombatAction currentAction = CombatAction.None;
        public float phaseElapsedMs = 0f;
        public float actionElapsedMs = 0f;

        public bool isGuarding = false;
        public bool isParry = false;
        public bool isParryWindowOpen = false;
        public bool hasResolvedThisAction = false;
        public bool isMoving = false;
    }
}