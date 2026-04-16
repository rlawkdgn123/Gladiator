using Game.AI.Brains;
using Game.Combat.AI;
using Game.Combat.State;
using Game.Combat.Systems;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Animation.Bridges;
using UnityEngine;
using System.Diagnostics;
using Game.QA.Profiling;


namespace Game.Combat.Execution
{
    public class EnemyCombatController : MonoBehaviour
    {
        [Header("Combat State")]
        [SerializeField] CombatState combatState = new CombatState();

        [Header("Default AI Settings")]
        [SerializeField] AIPersonalityType defaultPersonality = AIPersonalityType.Default;
        [SerializeField] AIDifficultyType defaultDifficulty = AIDifficultyType.Normal;

        [Header("Parry Learning")]
        [SerializeField] AIParryLearner parryLearner = new AIParryLearner();

        [Header("Movement")]
        [SerializeField] Transform playerTransform;
        [SerializeField] float attackRange = 2.0f;
        [SerializeField] float moveSpeed = 3.0f;

        [Header("References")]
        [SerializeField] EnemyAnimatorBridge animatorBridge;

        IAIBrain currentBrain;
        Rigidbody rb;
        float decisionTimer;

        public CombatState State => combatState;
        public AIParryLearner ParryLearner => parryLearner;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();

            combatState.personality = defaultPersonality;
            combatState.difficulty = defaultDifficulty;

            SetBrain(BrainType.Utility);
            parryLearner.SetDifficulty(defaultDifficulty);
            ResetDecisionTimer();
        }

        void Update()
        {
            if (!combatState.useAnimationEventPhaseSync)
                ActionPhaseDriver.Tick(combatState.enemy, Time.deltaTime);
            else if (combatState.enemy.currentAction != CombatAction.None)
                combatState.enemy.actionElapsedMs += Time.deltaTime * 1000f;

            if (combatState.enableAutoRuntimeResolve)
                CombatRuntimeResolver.TryResolveEnemyAttackAgainstPlayer(combatState);

            // 거리 계산 후 distanceBucket 갱신
            UpdateDistanceBucket();

            // Approaching 모드면 매 프레임 플레이어 방향으로 이동
            if (combatState.currentTacticalMode == AITacticalMode.Approaching)
                MoveTowardPlayer();

            combatState.enemy.isMoving = (combatState.currentTacticalMode == AITacticalMode.Approaching);

            if (animatorBridge != null)
            {
                animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
                animatorBridge.ApplyParry(combatState.enemy.isParry);
                animatorBridge.ApplyMoving(combatState.enemy.isMoving);
            }

            if (combatState.enemy.currentPhase != CombatPhase.Idle)
                return;

            decisionTimer -= Time.deltaTime;

            if (decisionTimer <= 0f)
            {
                TickDecision();
                ResetDecisionTimer();
            }
        }

        void UpdateDistanceBucket()
        {
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            combatState.distanceBucket = dist <= attackRange ? 0 : 1;
        }

        void MoveTowardPlayer()
        {
            if (playerTransform == null || rb == null) return;
            if (combatState.enemy.currentPhase != CombatPhase.Idle) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange) return;

            Vector3 dir = (playerTransform.position - transform.position).normalized;
            dir.y = 0f;

            float speed = combatState.difficulty switch
            {
                AIDifficultyType.Easy   => moveSpeed * 0.6f,
                AIDifficultyType.Normal => moveSpeed,
                AIDifficultyType.Hard   => moveSpeed * 1.3f,
                _                       => moveSpeed
            };

            rb.MovePosition(rb.position + dir * speed * Time.deltaTime);

            // 플레이어 방향으로 회전
            if (dir != Vector3.zero)
                rb.MoveRotation(Quaternion.LookRotation(dir));
        }

        // 현재 상태를 Observation으로 변환 후 행동 결정, actionexecutor에 값 넣어주기
        public CombatAction TickDecisionAndGetAction()
        {
            EnsureBrainInitialized();

            if (currentBrain == null)
                return CombatAction.None;

            var observation = ObservationBuilder.Build(combatState, parryLearner);

            // BehaviorTreeBrain이면 모드도 같이 갱신
            if (currentBrain is BehaviorTreeBrain btBrain)
            {
                var trace = btBrain.GetTrace(in observation);
                combatState.currentTacticalMode = trace.Mode;
                var action = trace.Action;

                bool executed = ActionExecutor.TryExecute(combatState, action);
                if (executed && animatorBridge != null)
                {
                    animatorBridge.ApplyAction(action);
                    animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
                    animatorBridge.ApplyParry(combatState.enemy.isParry);
                }
                return action;
            }
            else
            {
                combatState.currentTacticalMode = AITacticalMode.None;
                var action = currentBrain.Decide(in observation);

                bool executed = ActionExecutor.TryExecute(combatState, action);
                if (executed && animatorBridge != null)
                {
                    animatorBridge.ApplyAction(action);
                    animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
                    animatorBridge.ApplyParry(combatState.enemy.isParry);
                }
                return action;
            }
        }

        public void TickDecision()
        {
            TickDecisionAndGetAction();
        }

        void EnsureBrainInitialized()
        {
            if (currentBrain != null)
                return;

            currentBrain = combatState.currentBrain switch
            {
                BrainType.Utility => new UtilityBrain(combatState.personality, combatState.difficulty),
                BrainType.BehaviorTree => new BehaviorTreeBrain(combatState.personality, combatState.difficulty),
                _ => null
            };
        }

        // 현재 사용할 BrainType 변경.
        public void SetBrain(BrainType brainType)
        {
            combatState.currentBrain = brainType;

            currentBrain = brainType switch
            {
                // 이후에는 behaviortree, ML, hybrid 등등 추가할거임.
                BrainType.Utility => new UtilityBrain(combatState.personality, combatState.difficulty),
                BrainType.BehaviorTree => new BehaviorTreeBrain(combatState.personality, combatState.difficulty),
                _ => null
            };
        }

        // AI 성향 변경 후 Brain 재생성.
        public void SetPersonality(AIPersonalityType newPersonality)
        {
            combatState.personality = newPersonality;
            SetBrain(combatState.currentBrain);
        }

        // AI 난이도 변경 후 Brain 재생성.
        public void SetDifficulty(AIDifficultyType newDifficulty)
        {
            combatState.difficulty = newDifficulty;
            parryLearner.SetDifficulty(newDifficulty);

            SetBrain(combatState.currentBrain);
            ResetDecisionTimer();
        }

        // ─── Parry Learner API ───────────────────────────────

        /// <summary>플레이어가 공격을 시도할 때 PlayerCombatController 등에서 호출.</summary>
        public void NotifyPlayerAttack(AttackDirection direction)
        {
            parryLearner.RecordPlayerAttack(direction);
        }

        /// <summary>AI가 들어오는 공격에 패링을 시도해야 하는지 판단.</summary>
        public bool ShouldAIParry(AttackDirection incomingDirection)
        {
            return parryLearner.RollParry(incomingDirection);
        }

        /// <summary>라운드 시작 등 학습 데이터 초기화.</summary>
        public void ResetParryLearner()
        {
            parryLearner.Reset();
        }

        // 난이도별 판단 주기 설정.        
        private void ResetDecisionTimer()
        {
            decisionTimer = combatState.difficulty switch
            {
                AIDifficultyType.Easy => Random.Range(0.45f, 0.55f),
                AIDifficultyType.Normal => Random.Range(0.35f, 0.45f),
                AIDifficultyType.Hard => Random.Range(0.15f, 0.25f),
                _ => 0.4f
            };
        }

        public DecisionProfileResult ProfileCurrentDecision()
        {
            var result = new DecisionProfileResult
            {
                BrainType = combatState.currentBrain.ToString(),
                Personality = combatState.personality.ToString(),
                Difficulty = combatState.difficulty.ToString(),
                Action = CombatAction.None.ToString(),
                EnemyDirection = combatState.enemy.currentDirection.ToString(),
                EnemyPhase = combatState.enemy.currentPhase.ToString()
            };

            EnsureBrainInitialized();

            if(currentBrain == null)
                return result;

            var totalWatch = Stopwatch.StartNew();

            var observationWatch = Stopwatch.StartNew();
            var observation = ObservationBuilder.Build(combatState, parryLearner);
            observationWatch.Stop();

            var decisionWatch = Stopwatch.StartNew();
            var action = currentBrain.Decide(in observation);
            decisionWatch.Stop();

            var executeWatch = Stopwatch.StartNew();
            bool executed = ActionExecutor.TryExecute(combatState, action);
            executeWatch.Stop();

            double animatorMs = 0.0;
            bool animatorApplied = false;

            if (executed && animatorBridge != null)
            {
                var animatorWatch = Stopwatch.StartNew();
                animatorBridge.ApplyAction(action);
                animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
                animatorBridge.ApplyParry(combatState.enemy.isParry);
                animatorWatch.Stop();

                animatorMs = animatorWatch.Elapsed.TotalMilliseconds;
                animatorApplied = true;
            }

            totalWatch.Stop();

            result.Action = action.ToString();
            result.EnemyDirection = combatState.enemy.currentDirection.ToString();
            result.EnemyPhase = combatState.enemy.currentPhase.ToString();
            result.Executed = executed;
            result.AnimatorApplied = animatorApplied;

            result.ObservationMs = observationWatch.Elapsed.TotalMilliseconds;
            result.DecisionMs = decisionWatch.Elapsed.TotalMilliseconds;
            result.ExecuteMs = executeWatch.Elapsed.TotalMilliseconds;
            result.AnimatorMs = animatorMs;
            result.TotalMs = totalWatch.Elapsed.TotalMilliseconds;

            return result;
        }
    }
}