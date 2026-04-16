using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Core.Types;

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

            CombatAction bestAction = CombatAction.Wait;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                if (candidate.Score > bestScore)
                {
                    bestScore = candidate.Score;
                    bestAction = candidate.Action;
                }
            }

            return bestAction;
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
                new ActionCandidateScore(CombatAction.Wait, ScoreWait(observation))
            };
        }

        float ScoreHeavyAttack(AttackDirection direction, float enemyGuardHealth, in CombatObservation observation)
        {
            // 기본 점수
            float score = 1.0f;

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

        float GetPersonalityAttackBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => 1.0f,
                AIPersonalityType.Defensive => -0.5f,
                _ => 0f
            };
        }

        float GetPersonalityGuardBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => -0.2f,
                AIPersonalityType.Defensive => 0.8f,
                _ => 0f
            };
        }

        float GetPersonalityWaitBias()
        {
            return personality switch
            {
                AIPersonalityType.Aggressive => -0.15f,
                AIPersonalityType.Defensive => 0.3f,
                _ => 0f
            };
        }
    }
}