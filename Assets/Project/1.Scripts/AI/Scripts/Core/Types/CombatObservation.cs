using Game.Core.Enums;

namespace Game.Core.Types
{
    public struct CombatObservation
    {
        public float MyHp;
        public float EnemyHp;

        public float MyTopGH;
        public float MyLeftGH;
        public float MyRightGH;

        public float EnemyTopGH;
        public float EnemyLeftGH;
        public float EnemyRightGH;

        public bool MyLeftArmInjured;
        public bool MyRightArmInjured;
        public bool MyLeftLegInjured;
        public bool MyRightLegInjured;
        public bool MyBodyInjured;
        public bool MyHeadInjured;

        public bool EnemyLeftArmInjured;
        public bool EnemyRightArmInjured;
        public bool EnemyLeftLegInjured;
        public bool EnemyRightLegInjured;
        public bool EnemyBodyInjured;
        public bool EnemyHeadInjured;

        public float MyHype;
        public float EnemyHype;

        public int DistanceBucket;
        public bool HasFrameAdvantage;

        public AttackDirection RecentAttackA;
        public AttackDirection RecentAttackB;
        public AttackDirection RecentAttackC;

        public CombatPhase CurrentPhase;
        public BrainType CurrentBrain;

        public AttackDirection EnemyCurrentDirection;

        public float MyParryRateTop;
        public float MyParryRateLeft;
        public float MyParryRateRight;

        public bool MyIsGuarding;
        public bool MyIsParry;
        public bool EnemyIsGuarding;
        public bool EnemyIsParry;
    }
}