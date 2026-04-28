using System;

namespace Game.QA.Logging
{
    [Serializable]
    public class EpisodeStepLog
    {
        public int Step;
        public string Action;
        public string EnemyDirection;
        public string EnemyPhase;

        public float PlayerHp;
        public float EnemyHp;

        public string BrainType;
        public string Personality;
        public string Difficulty;

        public float ValidHit;
        public float GuardPressure;
        public float GuardBreakBonus;
        public float InjuryBonus;
        public float ShowmanshipBonus;
        public float RepetitionPenalty;
        public float DistanceControlBonus;
        public float TotalReward;
    }
}