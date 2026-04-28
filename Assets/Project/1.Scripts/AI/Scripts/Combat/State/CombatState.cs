using Game.Core.Enums;

namespace Game.Combat.State
{
    [System.Serializable]
    public class CombatState
    {
        public FighterState player = new();
        public FighterState enemy = new();

        public int distanceBucket = 1;
        // 실거리(미터). EnemyCombatController.Update에서 매 프레임 갱신.
        public float distanceMeters;
        // 캐릭터의 attackRange(미터). 같이 갱신해두면 brain에서 비율 계산 가능.
        public float attackRangeMeters = 1.8f;
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