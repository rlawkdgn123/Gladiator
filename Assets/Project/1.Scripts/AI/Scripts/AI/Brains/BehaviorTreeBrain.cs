using System.Collections.Generic;
using Game.AI.BehaviorTree;
using Game.AI.BehaviorTree.Nodes;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Core.Types;
using UnityEngine;

// 1. BT로 전술 모드 결정
// 2.그 모드에 맞는 UtilityBrain으로 세부 행동 선택

namespace Game.AI.Brains
{
    public class BehaviorTreeBrain : IAIBrain
    {
        readonly BTNode root;
        readonly UtilityBrain defensiveBrain;
        readonly UtilityBrain pressureBrain;
        readonly UtilityBrain neutralBrain;
        readonly AIDifficultyType difficulty;

        public BehaviorTreeBrain(AIPersonalityType personality, AIDifficultyType difficulty)
        {
            this.difficulty    = difficulty;
            defensiveBrain     = new UtilityBrain(AIPersonalityType.Defensive, difficulty);
            pressureBrain      = new UtilityBrain(AIPersonalityType.Aggressive, difficulty);
            neutralBrain       = new UtilityBrain(personality, difficulty);
            root = BuildTree();
        }

        public CombatAction Decide(in CombatObservation observation)
        {
            return GetTrace(in observation).Action;
        }
        public BehaviorTraceResult GetTrace(in CombatObservation observation)
        {
            var context = new BTContext(observation);
            root.Evaluate(context);

            var obs = context.Observation;

            // Approaching 모드면 행동 결정 스킵 (이동만)
            if (context.Mode == AITacticalMode.Approaching)
            {
                return new BehaviorTraceResult
                {
                    Mode = context.Mode,
                    Action = CombatAction.Wait,
                    TraceLines = context.TraceLines
                };
            }

            UtilityBrain brain = context.Mode switch
            {
                AITacticalMode.Defensive => defensiveBrain,
                AITacticalMode.Pressure  => pressureBrain,
                _                        => neutralBrain
            };

            var candidates = brain.GetCandidates(in obs);

            // Hard 난이도: 범위 내 진입 시 능동적으로 최적 가드 방향 강제
            if (difficulty == AIDifficultyType.Hard)
                OverrideGuardDirectionForHard(candidates, in obs);

            CombatAction action = SelectBest(candidates);

            return new BehaviorTraceResult
            {
                Mode = context.Mode,
                Action = action,
                TraceLines = context.TraceLines
            };
        }

        // Hard 전용: 플레이어 공격 패턴 읽어서 해당 방향 가드 점수 대폭 상승
        void OverrideGuardDirectionForHard(List<ActionCandidateScore> candidates, in CombatObservation obs)
        {
            var bestGuardDir = GetMostFrequentPlayerAttackDir(in obs);
            if (bestGuardDir == AttackDirection.None) return;

            CombatAction bestGuard = bestGuardDir switch
            {
                AttackDirection.Top   => CombatAction.GuardTop,
                AttackDirection.Left  => CombatAction.GuardLeft,
                AttackDirection.Right => CombatAction.GuardRight,
                _                     => CombatAction.None
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Action == bestGuard)
                {
                    var c = candidates[i];
                    c.Score += 1.5f;
                    candidates[i] = c;
                }
            }
        }

        // recentAttack + ParryRate 조합으로 플레이어 가장 많이 쓰는 방향 계산
        AttackDirection GetMostFrequentPlayerAttackDir(in CombatObservation obs)
        {
            // ParryRate가 가장 높은 방향 = 학습상 플레이어가 가장 많이 쓴 방향
            float top   = obs.MyParryRateTop;
            float left  = obs.MyParryRateLeft;
            float right = obs.MyParryRateRight;

            if (top == 0f && left == 0f && right == 0f)
            {
                // 학습 데이터 없으면 recentAttack 기반으로 판단
                return GetMostFrequentRecent(obs.RecentAttackA, obs.RecentAttackB, obs.RecentAttackC);
            }

            if (top >= left && top >= right)   return AttackDirection.Top;
            if (left >= top && left >= right)  return AttackDirection.Left;
            return AttackDirection.Right;
        }

        AttackDirection GetMostFrequentRecent(AttackDirection a, AttackDirection b, AttackDirection c)
        {
            int top = 0, left = 0, right = 0;
            foreach (var d in new[] { a, b, c })
            {
                if (d == AttackDirection.Top)   top++;
                else if (d == AttackDirection.Left)  left++;
                else if (d == AttackDirection.Right) right++;
            }
            if (top == 0 && left == 0 && right == 0) return AttackDirection.None;
            if (top >= left && top >= right)   return AttackDirection.Top;
            if (left >= top && left >= right)  return AttackDirection.Left;
            return AttackDirection.Right;
        }

        CombatAction SelectBest(List<ActionCandidateScore> candidates)
        {
            CombatAction best = CombatAction.Wait;
            float bestScore = float.MinValue;
            var tiedActions = new List<CombatAction>();

            foreach (var c in candidates)
            {
                if (c.Score > bestScore + 0.0001f)
                {
                    bestScore = c.Score;
                    best = c.Action;
                    tiedActions.Clear();
                    tiedActions.Add(c.Action);
                    continue;
                }

                if (Mathf.Abs(c.Score - bestScore) <= 0.0001f)
                    tiedActions.Add(c.Action);
            }

            if (tiedActions.Count == 0)
                return best;

            return tiedActions[Random.Range(0, tiedActions.Count)];
        }

        BTNode BuildTree()
        {
            return new SelectorNode(new List<BTNode>
            {
                // 최우선: 공격 범위 밖이면 Approaching
                new SequenceNode(new List<BTNode>
                {
                    new ConditionNode("OutOfRange", ctx => ctx.Observation.DistanceBucket > 0),
                    new SetModeNode(AITacticalMode.Approaching)
                }),
                // 범위 내 전술 판단
                new SequenceNode(new List<BTNode>
                {
                    new ConditionNode("MyHype <= 20", ctx => ctx.Observation.MyHype <= 20f),
                    new SetModeNode(AITacticalMode.Defensive)
                }),
                new SequenceNode(new List<BTNode>
                {
                    new ConditionNode("HasFrameAdvantage", ctx => ctx.Observation.HasFrameAdvantage),
                    new SetModeNode(AITacticalMode.Pressure)
                }),
                new SetModeNode(AITacticalMode.Neutral)
            });
        }
    }
}
