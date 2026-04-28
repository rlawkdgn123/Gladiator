using System;
using Game.Core.Enums;

namespace Game.Core.Types
{
    [Serializable]
    public class DefenseResolveResult
    {
        public DefenseResultType ResultType = DefenseResultType.None;
        public CombatAction AttackAction = CombatAction.None;
        public AttackDirection AttackDirection = AttackDirection.None;
        public bool DirectionMatched = false;
        public bool WasParryWindow = false;
        public int Damage = 0;
    }
}