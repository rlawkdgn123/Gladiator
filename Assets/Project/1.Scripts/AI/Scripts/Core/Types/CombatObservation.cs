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
        // 실거리 (미터). 0 이상.
        public float DistanceMeters;
        // attackRange를 넘어선 거리 (미터). 사거리 안이면 0, 밖이면 양수.
        public float DistanceOverRange;
        // 캐릭터의 attackRange (미터). 보너스/패널티 정규화용.
        public float AttackRangeMeters;
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