using Game.Core.Enums;

namespace Game.Core.Types
{
    public struct CombatSnapshotData
    {
        public string ObjectName;
        public float Hp;

        public float TopGH;
        public float LeftGH;
        public float RightGH;

        public GuardTier TopTier;
        public GuardTier LeftTier;
        public GuardTier RightTier;

        public bool LeftArmInjured;
        public bool RightArmInjured;
        public bool LeftLegInjured;
        public bool RightLegInjured;
        public bool BodyInjured;
        public bool HeadInjured;

        public float Hype;

        public AttackDirection CurrentDirection;
        public CombatPhase CurrentPhase;
        public BrainType BrainType;
    }
}