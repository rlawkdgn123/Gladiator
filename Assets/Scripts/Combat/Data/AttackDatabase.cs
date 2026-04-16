using System.Collections.Generic;
using Game.Core.Enums;


// 공격 행동 값. frame이나 damage 바뀌면 수정해줘야함.
namespace Game.Combat.Data
{
    public static class AttackDatabase
    {
        static readonly Dictionary<CombatAction, AttackData> _attacks = new()
        {
            {
                CombatAction.AttackTopHeavy,
                new AttackData
                {
                    Id = "heavy_top_opener",
                    Action = CombatAction.AttackTopHeavy,
                    Direction = AttackDirection.Top,
                    StartupMs = 800f,
                    ActiveMs = 100f,
                    RecoveryMs = 500f,
                    ParryWindowStartMs = 650f,
                    ParryWindowEndMs = 820f,
                    Damage = 24
                }
            },
            {
                CombatAction.AttackLeftHeavy,
                new AttackData
                {
                    Id = "heavy_left_opener",
                    Action = CombatAction.AttackLeftHeavy,
                    Direction = AttackDirection.Left,
                    StartupMs = 800f,
                    ActiveMs = 100f,
                    RecoveryMs = 500f,
                    ParryWindowStartMs = 650f,
                    ParryWindowEndMs = 820f,
                    Damage = 27
                }
            },
            {
                CombatAction.AttackRightHeavy,
                new AttackData
                {
                    Id = "heavy_right_opener",
                    Action = CombatAction.AttackRightHeavy,
                    Direction = AttackDirection.Right,
                    StartupMs = 800f,
                    ActiveMs = 100f,
                    RecoveryMs = 500f,
                    ParryWindowStartMs = 650f,
                    ParryWindowEndMs = 820f,
                    Damage = 27
                }
            }
        };

        public static bool TryGet(CombatAction action, out AttackData data)
        {
            return _attacks.TryGetValue(action, out data);
        }
    }
}