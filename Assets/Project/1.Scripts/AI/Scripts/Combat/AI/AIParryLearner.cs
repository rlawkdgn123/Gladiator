using System.Collections.Generic;
using System.Linq;
using Game.Core.Enums;
using UnityEngine;

namespace Game.Combat.AI
{
    /// <summary>
    /// 방향별 패링 확률을 추적하고 플레이어 공격 패턴을 학습한다.
    ///
    /// 동작 규칙:
    ///  - 같은 방향 연속 공격 → 해당 방향 패링 확률 누적 상승
    ///  - 방향이 바뀌면 → 새 방향의 카운터를 0으로 리셋 (다른 방향 카운터는 유지)
    ///  - Queue 기반 패턴 분석 → 빈도/교대 패턴 감지 시 추가 보너스
    ///  - 난이도별 base rate 스케일링
    /// </summary>
    [System.Serializable]
    public class AIParryLearner
    {
        [Header("Base Parry Rate by Difficulty")]
        [SerializeField] float easyBaseRate   = 0.10f;  // 10%
        [SerializeField] float normalBaseRate = 0.20f;  // 20%
        [SerializeField] float hardBaseRate   = 0.50f;  // 50%

        [Header("Learning Bonuses")]
        [Tooltip("+% per consecutive same-direction attack")]
        [SerializeField] float consecutiveBonus   = 0.10f;  // 연속 공격당 +10%

        [Tooltip("+% per frequency tier (frequencyStep 단위)")]
        [SerializeField] float frequencyBonus     = 0.05f;  // 빈도 티어당 +5%

        [Tooltip("frequency tier 기준 (history 내 점유율)")]
        [SerializeField] float frequencyStep      = 0.30f;  // 30% 점유마다 1티어

        [Tooltip("+% when A-B-A-B alternating pattern detected")]
        [SerializeField] float alternatingBonus   = 0.08f;  // 교대 패턴 감지 시 +8%

        [Header("Consecutive Decay on Direction Switch")]
        [Tooltip("방향 전환 시 이전 방향 consecutive count에 곱할 감쇠 계수 (0=즉시 리셋, 1=유지)")]
        [Range(0f, 1f)]
        [SerializeField] float consecutiveDecayFactor = 0.5f;  // 0.5 = 절반으로 줄이기

        [Header("Caps & History")]
        [SerializeField] float maxParryRate = 0.95f;        // 최대 95%
        [SerializeField] int   historySize  = 10;           // 기억할 최근 공격 수

        // --- Runtime State ---
        AIDifficultyType difficulty = AIDifficultyType.Normal;

        // 방향별 연속 카운터 (방향이 바뀌면 새 방향만 0으로 리셋)
        Dictionary<AttackDirection, int> consecutiveCounts = new();

        // 최근 공격 히스토리 Queue
        Queue<AttackDirection> history = new();

        AttackDirection lastDirection = AttackDirection.None;

        // ─────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────

        public void SetDifficulty(AIDifficultyType diff)
        {
            difficulty = diff;
        }

        /// <summary>플레이어가 공격을 시도할 때 호출. 학습 카운터 갱신.</summary>
        public void RecordPlayerAttack(AttackDirection dir)
        {
            if (dir == AttackDirection.None) return;

            // 방향이 바뀌면:
            //  - 이전 방향 consecutive count를 decayFactor만큼 감쇠
            //  - 새 방향 consecutive count는 0으로 리셋 후 누적 시작
            if (dir != lastDirection)
            {
                if (lastDirection != AttackDirection.None && consecutiveCounts.ContainsKey(lastDirection))
                {
                    int decayed = Mathf.FloorToInt(consecutiveCounts[lastDirection] * consecutiveDecayFactor);
                    consecutiveCounts[lastDirection] = decayed;
                }
                consecutiveCounts[dir] = 0;
            }

            consecutiveCounts[dir] = consecutiveCounts.GetValueOrDefault(dir, 0) + 1;

            history.Enqueue(dir);
            if (history.Count > historySize)
                history.Dequeue();

            lastDirection = dir;
        }

        /// <summary>해당 방향 공격에 대한 현재 패링 확률 (0~maxParryRate).</summary>
        public float GetParryRate(AttackDirection dir)
        {
            float rate = GetBaseRate()
                       + GetConsecutiveBonus(dir)
                       + GetPatternBonus(dir);

            return Mathf.Clamp(rate, 0f, maxParryRate);
        }

        /// <summary>패링 시도 롤. true면 패링 성공.</summary>
        public bool RollParry(AttackDirection dir)
        {
            return Random.value < GetParryRate(dir);
        }

        /// <summary>학습 데이터 전체 초기화 (라운드 리셋 등에 사용).</summary>
        public void Reset()
        {
            consecutiveCounts.Clear();
            history.Clear();
            lastDirection = AttackDirection.None;
        }

        // ─────────────────────────────────────────
        // Debug snapshot (CLI probe용)
        // ─────────────────────────────────────────

        public AIParryDebugSnapshot GetDebugSnapshot()
        {
            return new AIParryDebugSnapshot
            {
                difficulty          = difficulty.ToString(),
                baseRate            = GetBaseRate(),
                decayFactor         = consecutiveDecayFactor,
                lastDirection       = lastDirection.ToString(),
                historyCount        = history.Count,
                historySnapshot     = history.Select(d => d.ToString()).ToArray(),
                rates = new DirectionRates
                {
                    top   = GetParryRate(AttackDirection.Top),
                    left  = GetParryRate(AttackDirection.Left),
                    right = GetParryRate(AttackDirection.Right)
                },
                consecutiveCounts = new DirectionCounts
                {
                    top   = consecutiveCounts.GetValueOrDefault(AttackDirection.Top,   0),
                    left  = consecutiveCounts.GetValueOrDefault(AttackDirection.Left,  0),
                    right = consecutiveCounts.GetValueOrDefault(AttackDirection.Right, 0)
                }
            };
        }

        // ─────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────

        float GetBaseRate() => difficulty switch
        {
            AIDifficultyType.Easy => easyBaseRate,
            AIDifficultyType.Hard => hardBaseRate,
            _                     => normalBaseRate
        };

        float GetConsecutiveBonus(AttackDirection dir)
        {
            int count = consecutiveCounts.GetValueOrDefault(dir, 0);
            return count * consecutiveBonus;
        }

        float GetPatternBonus(AttackDirection dir)
        {
            if (history.Count < 2) return 0f;

            float bonus = 0f;
            var arr = history.ToArray();

            // 빈도 보너스: 히스토리에서 이 방향이 차지하는 비율
            int dirCount = arr.Count(d => d == dir);
            float freq = (float)dirCount / arr.Length;
            int freqTiers = Mathf.FloorToInt(freq / frequencyStep);
            bonus += freqTiers * frequencyBonus;

            // 교대 패턴 보너스: A-B-A-B 감지
            if (arr.Length >= 4 && IsAlternatingPattern(arr, dir))
                bonus += alternatingBonus;

            return bonus;
        }

        /// <summary>마지막 4개 공격이 A-B-A-B 교대 패턴이고 dir이 포함되면 true.</summary>
        bool IsAlternatingPattern(AttackDirection[] arr, AttackDirection dir)
        {
            int len = arr.Length;
            var last4 = arr[(len - 4)..];

            // last4[0]==last4[2], last4[1]==last4[3], last4[0]!=last4[1]
            bool isAlt = last4[0] == last4[2]
                      && last4[1] == last4[3]
                      && last4[0] != last4[1];

            return isAlt && (last4[0] == dir || last4[1] == dir);
        }
    }

    // ─────────────────────────────────────────
    // Debug DTOs
    // ─────────────────────────────────────────

    public class AIParryDebugSnapshot
    {
        public string   difficulty;
        public float    baseRate;
        public float    decayFactor;
        public string   lastDirection;
        public int      historyCount;
        public string[] historySnapshot;
        public DirectionRates  rates;
        public DirectionCounts consecutiveCounts;
    }

    public class DirectionRates
    {
        public float top;
        public float left;
        public float right;
    }

    public class DirectionCounts
    {
        public int top;
        public int left;
        public int right;
    }
}
