using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Core.Types;
using UnityEngine;

namespace Game.AI.Brains
{
    // ai 기본 판단 클래스임. 
    // combatobservation 입력받아서 행동마다 각각 점수를 계산 후 점수 높은 행동 선택.
    public class UtilityBrain : IAIBrain
    {
        readonly AIPersonalityType personality;
        readonly AIDifficultyType difficulty;

        public UtilityBrain(AIPersonalityType personality, AIDifficultyType difficulty)
        {
            this.personality = personality;
            this.difficulty = difficulty;
        }

        public CombatAction Decide(in CombatObservation observation)
        {
            // 리스트 생성 및 각각 행동마다 점수를 계산해서 제일 점수 높은 행동 반환
            var candidates = BuildCandidates(in observation);
            return SelectBest(candidates);
        }

        // 점수표 넘겨줄려고.
        public List<ActionCandidateScore> GetCandidates(in CombatObservation observation)
        {
            return BuildCandidates(in observation);
        }


        // 행동 리스트 생성해서 넘겨줄려고.
        List<ActionCandidateScore> BuildCandidates(in CombatObservation observation)
        {
            return new List<ActionCandidateScore>
            {
                // 상 좌 우 공격
                new ActionCandidateScore
                (
                    CombatAction.AttackTopHeavy,
                    ScoreHeavyAttack(AttackDirection.Top, observation.EnemyTopGH, observation)
                ),
                new ActionCandidateScore
                (
                    CombatAction.AttackLeftHeavy,
                    ScoreHeavyAttack(AttackDirection.Left, observation.EnemyLeftGH, observation)
                ),
                new ActionCandidateScore
                (
                    CombatAction.AttackRightHeavy,
                    ScoreHeavyAttack(AttackDirection.Right, observation.EnemyRightGH, observation)
                ),

                // 상 좌 우 가드 및 대기.
                new ActionCandidateScore(CombatAction.GuardTop, ScoreGuard(AttackDirection.Top, observation)),
                new ActionCandidateScore(CombatAction.GuardLeft, ScoreGuard(AttackDirection.Left, observation)),
                new ActionCandidateScore(CombatAction.GuardRight, ScoreGuard(AttackDirection.Right, observation)),
                new ActionCandidateScore(CombatAction.ParryTop, ScoreParry(AttackDirection.Top, observation)),
                new ActionCandidateScore(CombatAction.ParryLeft, ScoreParry(AttackDirection.Left, observation)),
                new ActionCandidateScore(CombatAction.ParryRight, ScoreParry(AttackDirection.Right, observation)),
                new ActionCandidateScore(CombatAction.Wait, ScoreWait(observation))
            };
        }

        static CombatAction SelectBest(List<ActionCandidateScore> candidates)
        {
            CombatAction bestAction = CombatAction.Wait;
            float bestScore = float.MinValue;
            var tiedActions = new List<CombatAction>();

            foreach (var candidate in candidates)
            {
                if (candidate.Score > bestScore + 0.0001f)
                {
                    bestScore = candidate.Score;
                    bestAction = candidate.Action;
                    tiedActions.Clear();
                    tiedActions.Add(candidate.Action);
                    continue;
                }

                if (Mathf.Abs(candidate.Score - bestScore) <= 0.0001f)
                    tiedActions.Add(candidate.Action);
            }

            if (tiedActions.Count == 0)
                return bestAction;

            return tiedActions[Random.Range(0, tiedActions.Count)];
        }

        // 사거리 밖에서도 어디까지 공격을 시도할 수 있게 둘지 (m). 이 거리를 넘으면 -10 컷.
        // attackRange + lungeReach 까지가 공격 가능 영역, 그 안에서는 거리에 비례한 페널티.
        const float LungeReachMeters = 0.6f;

        float ScoreHeavyAttack(AttackDirection direction, float enemyGuardHealth, in CombatObservation observation)
        {
            // 사거리에서 LungeReach 이상 멀면 공격 자체 불가
            float distOver = observation.DistanceOverRange;
            if (distOver > LungeReachMeters)
                return -10f;

            // 기본 점수
            float score = 1.0f;

            // 사거리 안(distOver==0)이면 페널티 0, 밖이면 거리 비례 선형 감점.
            // LungeReach 끝(=경계)에서 -1.5 페널티. Aggressive 보정(+2.5)이면 여전히 양수 가능.
            float distancePenalty = -1.5f * (distOver / LungeReachMeters);
            score += distancePenalty;

            // 상대 GH가 약하면 그 방향 더 노리기.
            score += GetWeakGuardBonus(enemyGuardHealth);

            // 프레임 우위면 압박
            score += GetFrameAdvantageBonus(observation);

            // 이전과 다른 방향 사용해서 쇼맨십 / 다양성 보너스
            score += GetShowmanshipBonus(direction, observation);

            // 부상 부위 노리기 보너스 (Normal 이상만)
            score += GetInjuryTargetBonus(direction, in observation);

            // 성향에 따른 공격 보정
            score += GetPersonalityAttackBias();

            return score;
        }

        float ScoreGuard(AttackDirection direction, in CombatObservation observation)
        {
            float score = 0.5f;

            // 적 공격 방향과 일치하면 보너스
            if (direction == observation.EnemyCurrentDirection &&
                observation.EnemyCurrentDirection != AttackDirection.None)
                score += 1.0f;

            // 패링 학습 확률이 높은 방향이면 가드 가치 상승
            float parryRate = direction switch
            {
                AttackDirection.Top   => observation.MyParryRateTop,
                AttackDirection.Left  => observation.MyParryRateLeft,
                AttackDirection.Right => observation.MyParryRateRight,
                _                     => 0f
            };
            score += parryRate * 0.5f;

            // current hype이 낮으면 가드 보너스
            if (observation.MyHype <= 20f)
                score += GetLowHypeGuardBonus();

            // 성향에 따른 가드 보정
            score += GetPersonalityGuardBias();
            return score;
        }

        float ScoreParry(AttackDirection direction, in CombatObservation observation)
        {
            if (observation.EnemyCurrentDirection == AttackDirection.None)
                return -2f;

            float score = direction == observation.EnemyCurrentDirection ? 1.3f : -1.2f;

            float parryRate = direction switch
            {
                AttackDirection.Top   => observation.MyParryRateTop,
                AttackDirection.Left  => observation.MyParryRateLeft,
                AttackDirection.Right => observation.MyParryRateRight,
                _                     => 0f
            };

            score += parryRate * GetDifficultyParryMultiplier();

            if (observation.EnemyIsGuarding)
                score -= 0.4f;

            score += GetPersonalityParryBias();
            return score;
        }

        float GetDifficultyParryMultiplier()
        {
            return difficulty switch
            {
                AIDifficultyType.Easy => 0.45f,
                AIDifficultyType.Normal => 1.0f,
                AIDifficultyType.Hard => 1.45f,
                _ => 0f
            };
        }

        float ScoreWait(in CombatObservation observation)
        {
            float score = 0.1f;

            // 프레임 우위 없으면 대기 보너스
            if (!observation.HasFrameAdvantage)
                score += 0.15f;

            // 난이도에 따라서 대기 보정
            score += GetDifficultyWaitBias();

            // 성향에 따라서 대기 보정
            score += GetPersonalityWaitBias();

            return score;
        }

        // 부상 부위 공략 보너스 (Easy 제외)
        // Top → 머리, Left → 오른팔(상대 기준), Right → 왼팔(상대 기준)
        // Body 부상 시 모든 공격에 소량 보너스
        float GetInjuryTargetBonus(AttackDirection direction, in CombatObservation observation)
        {
            if (difficulty == AIDifficultyType.Easy) return 0f;

            bool isDirectHit = direction switch
            {
                AttackDirection.Top   => observation.EnemyHeadInjured,
                AttackDirection.Left  => observation.EnemyRightArmInjured,
                AttackDirection.Right => observation.EnemyLeftArmInjured,
                _                     => false
            };

            float bonus = isDirectHit ? difficulty switch
            {
                AIDifficultyType.Normal => 0.7f,
                AIDifficultyType.Hard   => 1.2f,
                _                       => 0f
            } : 0f;

            if (observation.EnemyBodyInjured) bonus += 0.2f;

            return bonus;
        }

        // GH 약한 방향 보정 함수
        float GetWeakGuardBonus(float enemyGuardHealth)
        {
            // easy는 거의 못읽는 정도로. normal은 기본, hard 는 매우 잘 읽고 공략하는 쪽으로
            return difficulty switch
            {
                AIDifficultyType.Easy => enemyGuardHealth <= 25f ? 1.0f :
                                         enemyGuardHealth <= 60f ? 0.5f : 0f,

                AIDifficultyType.Normal => enemyGuardHealth <= 25f ? 2.0f :
                                           enemyGuardHealth <= 60f ? 1.0f : 0f,

                AIDifficultyType.Hard => enemyGuardHealth <= 25f ? 2.5f :
                                         enemyGuardHealth <= 60f ? 1.25f : 0f,
                _ => 0f     // 나머지 모든 경우를 뜻하는거임. c# 8.0부터 문법
            };
        }

        float GetFrameAdvantageBonus(in CombatObservation observation)
        {
            if (!observation.HasFrameAdvantage)
                return 0f;

            // 난이도에 따른 frame 우위 공격 보정
            return difficulty switch
            {
                AIDifficultyType.Easy => 0.25f,
                AIDifficultyType.Normal => 1.0f,
                AIDifficultyType.Hard => 1.5f,
                _ => 0f
            };
        }

        // 방향 다양성 계산 함수
        float GetShowmanshipBonus(AttackDirection direction, in CombatObservation observation)
        {   
            bool isNewDirection =
                direction != observation.RecentAttackA &&
                direction != observation.RecentAttackB &&
                direction != observation.RecentAttackC;

            // 난이도에 따라서 방향 다양성을 더 챙기는 쪽임.
            if (!isNewDirection)
            {
                return difficulty switch
                {
                    AIDifficultyType.Easy => -0.05f,
                    AIDifficultyType.Normal => -0.25f,
                    AIDifficultyType.Hard => -0.5f,
                    _ => 0f
                };
            }

            return difficulty switch
            {
                AIDifficultyType.Easy => 0.1f,
                AIDifficultyType.Normal => 0.5f,
                AIDifficultyType.Hard => 0.8f,
                _ => 0f
            };
        }

        float GetLowHypeGuardBonus()
        {
            return difficulty switch
            {
                AIDifficultyType.Easy => 0.25f,
                AIDifficultyType.Normal => 0.75f,
                AIDifficultyType.Hard => 1.0f,
                _ => 0f
            };
        }

        float GetDifficultyWaitBias()
        {
            return difficulty switch
            {
                AIDifficultyType.Easy => 0.6f,
                AIDifficultyType.Normal => 0.2f,
                AIDifficultyType.Hard => -0.1f,
                _ => 0f
            };
        }

        // 성향별 공격 가중치. 거리 페널티(-1.5)를 이길 수 있을 만큼 충분히 강하게.
        // Aggressive: +2.5 → LungeReach 경계에서도 양수 점수 유지 가능.
        // Default:    +0.6 → 사거리 안에선 적극적, 약간 멀면 망설임.
        // Defensive:  -1.0 → 사거리 안에서도 가드/대기 우선.
        float GetPersonalityAttackBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => 2.5f,
                AIPersonalityType.Defensive => -1.0f,
                AIPersonalityType.Default => 0.6f,
                _ => 0f
            };
        }

        float GetPersonalityGuardBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => -0.5f,
                AIPersonalityType.Defensive => 1.0f,
                AIPersonalityType.Default => 0.1f,
                _ => 0f
            };
        }

        float GetPersonalityParryBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => 0.35f,
                AIPersonalityType.Defensive => 0.75f,
                AIPersonalityType.Default => 0.2f,
                _ => 0f
            };
        }

        float GetPersonalityWaitBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => -0.45f,
                AIPersonalityType.Defensive => 0.4f,
                AIPersonalityType.Default => -0.05f,
                _ => 0f
            };
        }
    }
}
