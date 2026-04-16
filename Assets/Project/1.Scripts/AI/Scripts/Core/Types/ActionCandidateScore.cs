using Game.Core.Enums;

namespace Game.Core.Types
{
    public struct ActionCandidateScore
    {
        public CombatAction Action;
        public float Score;

        public ActionCandidateScore(CombatAction action, float score)
        {
            Action = action;
            Score = score;
        }
    }
}