using System;
using System.Collections.Generic;

namespace Game.QA.Logging
{
    [Serializable]
    public class EpisodeRunLog
    {
        public string Timestamp;
        public int TotalSteps;
        public string BrainType;
        public string Personality;
        public string Difficulty;

        public List<EpisodeStepLog> Steps = new();
    }
}