using UnityEngine;

namespace Game.Combat.Execution
{
    /// <summary>
    /// Binds two EnemyCombatController instances together and manages AI-vs-AI battle lifecycle.
    /// </summary>
    public class AIBattleManager : MonoBehaviour
    {
        [Header("Fighters")]
        [SerializeField] EnemyCombatController fighterA;
        [SerializeField] EnemyCombatController fighterB;

        [Header("Rules")]
        [Tooltip("A fighter is considered dead when HP is less than or equal to this value.")]
        [SerializeField] float deathThreshold = 0f;
        [Tooltip("Cooldown used before replaying hit reaction when HP drops repeatedly.")]
        [SerializeField] float hitCooldown = 0.2f;

        bool _battleRunning;
        bool _resultLogged;
        bool _aAlive;
        bool _bAlive;
        float _prevHpA;
        float _prevHpB;
        float _cooldownA;
        float _cooldownB;

        void Start()
        {
            if (fighterA == null || fighterB == null)
            {
                Debug.LogError("[AIBattleManager] FighterA / FighterB is not assigned.");
                return;
            }

            fighterA.BindOpponent(fighterB);
            fighterB.BindOpponent(fighterA);

            _prevHpA = fighterA.CurrentHp;
            _prevHpB = fighterB.CurrentHp;
            _aAlive = true;
            _bAlive = true;
            _battleRunning = true;

            Debug.Log($"[AIBattleManager] 전투 시작 - A:{fighterA.name} / B:{fighterB.name}");
        }

        void Update()
        {
            if (!_battleRunning)
                return;

            CheckHit(fighterA, fighterB, ref _prevHpB, ref _cooldownB);
            CheckHit(fighterB, fighterA, ref _prevHpA, ref _cooldownA);

            _cooldownA = Mathf.Max(0f, _cooldownA - Time.deltaTime);
            _cooldownB = Mathf.Max(0f, _cooldownB - Time.deltaTime);

            bool aJustDied = _aAlive && fighterA.CurrentHp <= deathThreshold;
            bool bJustDied = _bAlive && fighterB.CurrentHp <= deathThreshold;

            if (aJustDied)
            {
                _aAlive = false;
                fighterA.NotifyDie();
            }

            if (bJustDied)
            {
                _bAlive = false;
                fighterB.NotifyDie();
            }

            if (aJustDied || bJustDied)
                FinishBattle();
        }

        void CheckHit(EnemyCombatController attacker, EnemyCombatController victim, ref float prevHp, ref float cooldown)
        {
            if (attacker != null && attacker.UsesWeaponHitDetection)
            {
                prevHp = victim.CurrentHp;
                return;
            }

            float currentHp = victim.CurrentHp;

            if (currentHp < prevHp && cooldown <= 0f)
            {
                victim.NotifyHit(attacker.LastAttackDirection);
                cooldown = hitCooldown;
            }

            prevHp = currentHp;
        }

        void FinishBattle()
        {
            _battleRunning = false;

            if (_resultLogged)
                return;

            _resultLogged = true;

            if (!_aAlive && !_bAlive)
            {
                fighterA.NotifyBattleEnded();
                fighterB.NotifyBattleEnded();
                Debug.Log($"[AIBattleManager] 전투 종료 - 무승부 (A:{fighterA.CurrentHp:0.##} / B:{fighterB.CurrentHp:0.##})");
                return;
            }

            var winner = _aAlive ? fighterA : fighterB;
            var loser = _aAlive ? fighterB : fighterA;

            winner.NotifyBattleEnded();

            Debug.Log($"[AIBattleManager] 전투 종료 - 승자: {winner.name} / 패자: {loser.name} (A:{fighterA.CurrentHp:0.##} / B:{fighterB.CurrentHp:0.##})");
        }
    }
}
