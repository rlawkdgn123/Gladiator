using Game.Core.Enums;
using UnityEngine;

namespace Game.Combat.Execution
{
    /// <summary>
    /// 두 EnemyCombatController를 서로 연결해 AI vs AI 전투를 구성한다.
    ///
    /// 사용법:
    ///   1. 씬에 빈 GameObject를 만들고 이 컴포넌트를 추가.
    ///   2. FighterA / FighterB 슬롯에 두 EnemyCombatController 드래그.
    ///   3. Play → 자동 바인딩 후 전투 시작.
    /// </summary>
    public class AIBattleManager : MonoBehaviour
    {
        [Header("전투 참가자")]
        [SerializeField] EnemyCombatController fighterA;
        [SerializeField] EnemyCombatController fighterB;

        [Header("설정")]
        [Tooltip("HP가 이 값 이하로 내려가면 사망 처리")]
        [SerializeField] float deathThreshold = 0f;
        [Tooltip("연속 피격 방지 쿨다운 (초)")]
        [SerializeField] float hitCooldown = 0.2f;

        bool   _battleRunning;
        bool   _aAlive;
        bool   _bAlive;
        float  _prevHpA;
        float  _prevHpB;
        float  _cooldownA;
        float  _cooldownB;

        void Start()
        {
            if (fighterA == null || fighterB == null)
            {
                Debug.LogError("[AIBattleManager] FighterA / FighterB가 지정되지 않았습니다.");
                return;
            }

            // 상호 바인딩 — player 레퍼런스를 상대방의 enemy 상태로 교체
            fighterA.BindOpponent(fighterB);
            fighterB.BindOpponent(fighterA);

            _prevHpA = fighterA.CurrentHp;
            _prevHpB = fighterB.CurrentHp;
            _aAlive  = true;
            _bAlive  = true;

            _battleRunning = true;
            Debug.Log("[AIBattleManager] 전투 시작.");
        }

        void Update()
        {
            if (!_battleRunning) return;

            // 피격 감지: HP가 이전 프레임보다 줄었으면 피격 애니 트리거
            CheckHit(fighterA, fighterB, ref _prevHpB, ref _cooldownB);
            CheckHit(fighterB, fighterA, ref _prevHpA, ref _cooldownA);

            _cooldownA = Mathf.Max(0f, _cooldownA - Time.deltaTime);
            _cooldownB = Mathf.Max(0f, _cooldownB - Time.deltaTime);

            // 사망 판정
            if (_aAlive && fighterA.CurrentHp <= deathThreshold)
            {
                _aAlive = false;
                fighterA.NotifyDie();
                OnFighterDied(fighterA, fighterB);
            }

            if (_bAlive && fighterB.CurrentHp <= deathThreshold)
            {
                _bAlive = false;
                fighterB.NotifyDie();
                OnFighterDied(fighterB, fighterA);
            }
        }

        // attacker의 공격으로 victim HP가 감소했는지 확인 후 피격 애니 트리거
        void CheckHit(EnemyCombatController attacker, EnemyCombatController victim,
                      ref float prevHp, ref float cooldown)
        {
            float currentHp = victim.CurrentHp;

            if (currentHp < prevHp && cooldown <= 0f)
            {
                victim.NotifyHit(attacker.LastAttackDirection);
                cooldown = hitCooldown;
            }

            prevHp = currentHp;
        }

        void OnFighterDied(EnemyCombatController loser, EnemyCombatController winner)
        {
            _battleRunning = false;
            Debug.Log($"[AIBattleManager] 전투 종료 — 승자: {winner.name} / 패자: {loser.name}");
        }
    }
}
