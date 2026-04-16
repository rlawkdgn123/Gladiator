using Game.Core.Enums;

namespace Game.Combat.State
{
    [System.Serializable]
    public class CombatState
    {
        public FighterState player = new();
        public FighterState enemy = new();

        public int distanceBucket = 1;
        public bool hasFrameAdvantage;
        public BrainType currentBrain = BrainType.None;

        public AttackDirection recentAttackA = AttackDirection.None;
        public AttackDirection recentAttackB = AttackDirection.None;
        public AttackDirection recentAttackC = AttackDirection.None;

        public AIPersonalityType personality;
        public AIDifficultyType difficulty;

        public bool enableAutoRuntimeResolve = true;
        public bool useAnimationEventPhaseSync = true;

        public AITacticalMode currentTacticalMode = AITacticalMode.None;
    }
}