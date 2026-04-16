using System;
using System.Collections.Generic;
using Game.Combat.State;
using Game.Core.Types;

namespace Game.QA.Logging
{
    public static class CombatRuntimeLogger
    {
        static readonly List<CombatRuntimeLogEntry> entries = new();
        const int MaxEntries = 200;

        public static void Clear()
        {
            entries.Clear();
        }

        public static IReadOnlyList<CombatRuntimeLogEntry> GetEntries()
        {
            return entries;
        }

        public static void Add(CombatState state, DefenseResolveResult result)
        {
            if (state == null || state.player == null || state.enemy == null || result == null)
                return;

            entries.Add(new CombatRuntimeLogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                ResultType = result.ResultType.ToString(),
                AttackAction = result.AttackAction.ToString(),
                AttackDirection = result.AttackDirection.ToString(),
                DirectionMatched = result.DirectionMatched,
                WasParryWindow = result.WasParryWindow,
                Damage = result.Damage,

                PlayerHp = state.player.hp,
                EnemyHp = state.enemy.hp,

                PlayerDirection = state.player.currentDirection.ToString(),
                PlayerGuarding = state.player.isGuarding,
                PlayerParry = state.player.isParry,

                EnemyDirection = state.enemy.currentDirection.ToString(),
                EnemyPhase = state.enemy.currentPhase.ToString(),
                EnemyAction = state.enemy.currentAction.ToString(),
                EnemyActionElapsedMs = state.enemy.actionElapsedMs
            });

            if (entries.Count > MaxEntries)
                entries.RemoveAt(0);
        }
    }
}