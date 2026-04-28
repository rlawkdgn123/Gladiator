using System;

namespace Game.QA.Logging
{
    [Serializable]
    public class CombatRuntimeLogEntry
    {
        public string Timestamp;
        public string ResultType;
        public string AttackAction;
        public string AttackDirection;
        public bool DirectionMatched;
        public bool WasParryWindow;
        public int Damage;

        public float PlayerHp;
        public float EnemyHp;

        public string PlayerDirection;
        public bool PlayerGuarding;
        public bool PlayerParry;

        public string EnemyDirection;
        public string EnemyPhase;
        public string EnemyAction;
        public float EnemyActionElapsedMs;
    }
}