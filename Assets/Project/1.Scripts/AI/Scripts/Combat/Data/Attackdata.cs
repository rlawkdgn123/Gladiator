using System;
using Game.Core.Enums;
using UnityEngine;

namespace Game.Combat.Data
{
    [Serializable]
    public class AttackData
    {
        public string Id;
        public CombatAction Action;
        public AttackDirection Direction;

        public float StartupMs;
        public float ActiveMs;
        public float RecoveryMs;

        public float ParryWindowStartMs;
        public float ParryWindowEndMs;

        public int Damage;
    }
}