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
        [SerializeField] float moveSpeed = 1.5f;

        [Header("References")]
        [SerializeField] EnemyAnimatorBridge animatorBridge;

        [Header("Detection & Engagement")]
        [SerializeField] float detectionRange = 30f;  // 인지 범위: 이 안에 들어오면 Walk로 접근
        [SerializeField] float combatRange    = 5f;   // 전투 전환 범위: 이 안에 들어오면 Guard + AI 판단 시작

        // 거리 → 이동 가중치 커브 제어 (값이 낮을수록 가까울 때 이동 선호가 급격히 낮아짐)
        [SerializeField] [Range(0.1f, 1f)] float moveCurvePow = 0.35f;

        // ── Unaware  : playerTransform 없거나 detectionRange 밖
        // ── Approaching : detectionRange 이내, combatRange 밖 → Walk 접근 (판단 없음)
        // ── InCombat    : combatRange 이내 → Guard + AI 판단
        enum EngagementPhase { Unaware, Approaching, InCombat }
        EngagementPhase _engPhase    = EngagementPhase.Unaware;
        bool            _wantsToMove = false;   // 결정 틱마다 갱신, 프레임마다 유지

        IAIBrain currentBrain;
        Rigidbody rb;
        float decisionTimer;

        public CombatState State => combatState;
        public AIParryLearner ParryLearner => parryLearner;

        // ── AI vs AI 바인딩 ───────────────────────────────
        /// <summary>상대방 AI를 연결한다. AIBattleManager가 Start()에서 호출.</summary>
        public void BindOpponent(EnemyCombatController opponent)
        {
            playerTransform    = opponent.transform;
            combatState.player = opponent.State.enemy;   // 참조 공유 → 데미지 실시간 반영

            // 바인딩 직후 즉시 상대 방향으로 회전
            FaceTowardPlayer();
        }

        public float           CurrentHp            => combatState.enemy.hp;
        public AttackDirection LastAttackDirection   => combatState.enemy.currentDirection;

        public void NotifyHit(AttackDirection fromDirection)
        {
            animatorBridge?.ApplyHit(fromDirection);
        }

        public void NotifyDie()
        {
            enabled = false;                  // AI Update 루프 정지
            animatorBridge?.ApplyDie();
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Inspector에 연결 안 된 경우 자동 탐색
            if (animatorBridge == null)
                animatorBridge = GetComponentInChildren<EnemyAnimatorBridge>();

            combatState.personality = defaultPersonality;
            combatState.difficulty  = defaultDifficulty;

            SetBrain(BrainType.Utility);
            parryLearner.SetDifficulty(defaultDifficulty);
            ResetDecisionTimer();
        }

        void Update()
        {
            // ── 액션 페이즈 드라이버 ──────────────────────────
            if (!combatState.useAnimationEventPhaseSync)
                ActionPhaseDriver.Tick(combatState.enemy, Time.deltaTime);
            else if (combatState.enemy.currentAction != CombatAction.None)
                combatState.enemy.actionElapsedMs += Time.deltaTime * 1000f;

            if (combatState.enableAutoRuntimeResolve)
                CombatRuntimeResolver.TryResolveEnemyAttackAgainstPlayer(combatState);

            // ── Engagement Phase 갱신 ─────────────────────────
            UpdateEngagementPhase();

            bool isMoving   = false;
            bool isInCombat = (_engPhase == EngagementPhase.InCombat);

            switch (_engPhase)
            {
                // ── 1. Approaching: Guard 없이 Walk로 직진 ──────────────────
                case EngagementPhase.Approaching:
                {
                    isMoving = true;
                    _wantsToMove = false;             // 전투 판단 이동과 분리
                    combatState.distanceBucket = 1;
                    combatState.currentTacticalMode = AITacticalMode.Approaching;
                    MoveTowardPlayer();
                    FaceTowardPlayer();   // 이동 불가 상황에서도 항상 상대 바라보기
                    break;
                }

                // ── 2. InCombat: Guard 스탠스 + AI 판단 ──────────────────────
                case EngagementPhase.InCombat:
                {
                    float dist = playerTransform != null
                        ? Vector3.Distance(transform.position, playerTransform.position)
                        : 0f;

                    combatState.distanceBucket = dist <= attackRange ? 0 : 1;

                    // 결정 틱: Animator 트랜지션 중이면 보류 (phase 어긋남 방지)
                    bool animatorSettled = animatorBridge == null || !animatorBridge.IsInTransition();
                    if (combatState.enemy.currentPhase == CombatPhase.Idle && animatorSettled)
                    {
                        decisionTimer -= Time.deltaTime;
                        if (decisionTimer <= 0f)
                        {
                            // 이동 중: attackRange 도달 시에만 중지 (경계 진동 방지)
                            // 정지 중: 거리 기반 확률로 이동 여부 결정
                            if (_wantsToMove)
                            {
                                if (dist <= attackRange)
                                    _wantsToMove = false;
                            }
                            else
                            {
                                float t = Mathf.Clamp01(
                                    (dist - attackRange) / Mathf.Max(combatRange - attackRange, 0.01f));
                                float moveProb = Mathf.Pow(t, moveCurvePow);
                                _wantsToMove = dist > attackRange && Random.value < moveProb;
                            }

                            TickDecision();
                            ResetDecisionTimer();
                        }
                    }
                    else
                    {
                        _wantsToMove = false;
                    }

                    isMoving = _wantsToMove && dist > attackRange;
                    if (isMoving)
                        MoveTowardPlayer();
                    else
                        FaceTowardPlayer();

                    combatState.currentTacticalMode = isMoving ? AITacticalMode.Approaching : AITacticalMode.None;
                    break;
                }

                // ── 3. Unaware: Idle 대기 ──────────────────────────────────
                default:
                {
                    _wantsToMove = false;
                    combatState.currentTacticalMode = AITacticalMode.None;
                    combatState.distanceBucket = 1;
                    break;
                }
            }

            combatState.enemy.isMoving = isMoving;

            // ── Animator 갱신 ─────────────────────────────────
            if (animatorBridge != null)
            {
                animatorBridge.ApplyInCombat(isInCombat);
                animatorBridge.ApplyMoving(isMoving);
                animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
                animatorBridge.ApplyParry(combatState.enemy.isParry);
            }
        }

        void UpdateEngagementPhase()
        {
            if (playerTransform == null)
            {
                _engPhase = EngagementPhase.Unaware;
                return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (dist > detectionRange)
                _engPhase = EngagementPhase.Unaware;
            else if (dist > combatRange)
                _engPhase = EngagementPhase.Approaching;
            else
                _engPhase = EngagementPhase.InCombat;
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

        void FaceTowardPlayer()
        {
            if (playerTransform == null || rb == null) return;

            Vector3 dir = (playerTransform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
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