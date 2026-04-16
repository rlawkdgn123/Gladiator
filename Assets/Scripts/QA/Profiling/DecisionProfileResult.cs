using System;

namespace Game.QA.Profiling
{
    [Serializable]
    public class DecisionProfileResult
    {
        public string BrainType;
        public string Personality;
        public string Difficulty;

        public string Action;
        public string EnemyDirection;
        public string EnemyPhase;

        public bool Executed;
        public bool AnimatorApplied;

        public double ObservationMs;
        public double DecisionMs;
        public double ExecuteMs;
        public double AnimatorMs;
        public double TotalMs;
    }
}