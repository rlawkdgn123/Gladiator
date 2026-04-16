using Game.Core.Enums;

namespace Game.Combat.State
{
    [System.Serializable]
    public class GuardHealthState
    {
        public float top = 100f;
        public float left = 100f;
        public float right = 100f;

        public GuardTier GetTopTier() => ToTier(top);
        public GuardTier GetLeftTier() => ToTier(left);
        public GuardTier GetRightTier() => ToTier(right);

        GuardTier ToTier(float value)
        {
            if (value <= 0f) return GuardTier.Broken;
            if (value <= 25f) return GuardTier.Critical;
            if (value <= 60f) return GuardTier.Stable;
            return GuardTier.Full;
        }
    }
}