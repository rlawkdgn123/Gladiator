using System.Linq;
using Game.AI.Brains;
using Game.Combat.AI;
using Game.Combat.State;
using Game.Combat.Systems;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Core.Types;
using Game.Animation.Bridges;
using Game.Combat.HitDetection;
using Game.VFX.Data;
using Game.VFX.Runtime;
using UnityEngine;
using Game.QA.Profiling;
using Game.QA.Logging;

using Stopwatch = System.Diagnostics.Stopwatch;


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

        [Header("Parry Timing")]
        [SerializeField] float parryWindowDuration = 0.15f;
        [SerializeField] float parryRecoveryDuration = 0.35f;

        [Header("Movement")]
        [SerializeField] Transform playerTransform;
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float moveSpeed = 1.3f;
        [SerializeField] float stopDistanceBuffer = 0.05f;
        
        [Header("Physics Stability")]
        [SerializeField] bool freezeVerticalPosition = true;
        [SerializeField] bool freezeTiltRotation = true;
        [SerializeField] CollisionDetectionMode characterCollisionDetection = CollisionDetectionMode.ContinuousSpeculative;
        [SerializeField] RigidbodyInterpolation characterInterpolation = RigidbodyInterpolation.Interpolate;

        [Header("Hit Detection")]
        [SerializeField] bool useWeaponHitDetection = false;
        [Tooltip("씬에서 hand_r 본 밑에 직접 부착한 SwordPrefab의 SwordHitbox 컴포넌트. 자기 칼-자기 몸 무시용.")]
        [SerializeField] EnemyWeaponHitbox swordHitbox;
        [Tooltip("씬에 직접 부착된 SwordPrefab 루트 GameObject (SwordVFX / Blade_Tip 접근용).")]
        [SerializeField] GameObject equippedSwordObject;
        [Tooltip("캐릭터 본체 CapsuleCollider. 씬에서 직접 부착 (Is Trigger=false).")]
        [SerializeField] Collider bodyCollider;
        [Tooltip("히트 체인 단계마다 콘솔에 로그 (디버그 종료 후 false 권장).")]
        public bool verboseHitLogging = true;

        [Header("References")]
        [SerializeField] EnemyAnimatorBridge animatorBridge;

        [Header("Slash VFX")]
        [SerializeField] SlashProfileSO slashProfile;

        SwordVFXController _swordVFX;

        [Header("Detection & Engagement")]
        [SerializeField] float detectionRange = 30f;  // 인지 범위: 이 안에 들어오면 Walk로 접근
        [SerializeField] float combatRange    = 6f;   // 전투 전환 범위: 이 안에 들어오면 Guard + AI 판단 시작

        // 거리 → 이동 가중치 커브 제어 (값이 낮을수록 가까울 때 이동 선호가 급격히 낮아짐)
        [SerializeField] [Range(0.1f, 1f)] float moveCurvePow = 0.5f;
        [SerializeField] float mindGameRange = 2.1f;
        [SerializeField] [Range(0f, 1f)] float closeRangeMoveBias = 0.35f;

        [Header("Guard Walk Tuning")]
        [SerializeField] float guardWalkSpeedMultiplier = 0.8f;
        [SerializeField] float retreatDistance = 1.45f;
        [SerializeField] float strafeDistance = 2.6f;
        [SerializeField] [Range(0f, 1f)] float aggressiveStrafeChance = 0.12f;
        [SerializeField] [Range(0f, 1f)] float aggressiveRetreatChance = 0.03f;
        [SerializeField] [Range(0f, 1f)] float defaultStrafeChance = 0.34f;
        [SerializeField] [Range(0f, 1f)] float defaultRetreatChance = 0.16f;
        [SerializeField] [Range(0f, 1f)] float defensiveStrafeChance = 0.52f;
        [SerializeField] [Range(0f, 1f)] float defensiveRetreatChance = 0.50f;

        // ── Unaware  : playerTransform 없거나 detectionRange 밖
        // ── Approaching : detectionRange 이내, combatRange 밖 → Walk 접근 (판단 없음)
        // ── InCombat    : combatRange 이내 → Guard + AI 판단
        enum EngagementPhase { Unaware, Approaching, InCombat }
        EngagementPhase _engPhase    = EngagementPhase.Unaware;
        bool            _wantsToMove = false;   // 결정 틱마다 갱신, 프레임마다 유지

        IAIBrain currentBrain;
        Rigidbody rb;
        float decisionTimer;
        float lastMoveProbability;
        bool  lastForcedAdvance;
        AttackDirection lastObservedEnemyDirection = AttackDirection.None;
        GuardWalkDirection currentGuardWalkDirection = GuardWalkDirection.None;
        int consecutiveGuardWalkDecisions;

        // 공격/피격 직후 Idle로 들어온 후, 다음 결정까지 강제 대기 시간 (애니메이션 settle용).
        // 기본 0.35s — 너무 짧으면 1프레임만 idle 거치고 바로 다음 공격 → 애니메이션 어색.
        [SerializeField] float postActionIdleDwell = 0.35f;
        CombatPhase _lastObservedPhase = CombatPhase.Idle;
        float _idleEnteredTime = -999f;

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
        public float AttackRange => attackRange;
        public float CombatRange => combatRange;
        public float DetectionRange => detectionRange;
        public float MindGameRange => GetMindGameRange();
        public bool UsesWeaponHitDetection => useWeaponHitDetection;

        // 피격 시 결정/공격 보류 시간 (초). Hit 애니메이션 길이에 맞춰 조정.
        [Header("Hit Reaction")]
        [SerializeField] float hitStunDuration = 0.55f;

        // 피격 stun 중 플래그. ActionPhaseDriver/decision/ApplyAction을 모두 보류.
        float _hitStunUntil = -1f;
        bool IsHitStunned => Time.time < _hitStunUntil;

        public void NotifyHit(AttackDirection fromDirection)
        {
            if (verboseHitLogging)
                UnityEngine.Debug.Log($"[NotifyHit] {gameObject.name} took hit from {fromDirection}", this);

            // 피격 받은 쪽 상태를 Wait/Idle 로 set. 공격 phase를 끊는다.
            combatState.enemy.currentAction = CombatAction.Wait;
            combatState.enemy.currentPhase = CombatPhase.Idle;
            combatState.enemy.phaseElapsedMs = 0f;
            combatState.enemy.actionElapsedMs = 0f;
            combatState.enemy.hasResolvedThisAction = false;
            combatState.enemy.isParry = false;
            combatState.enemy.isGuarding = false;

            // 다음 결정/페이즈 진행을 stun 동안 완전히 차단.
            _hitStunUntil = Time.time + hitStunDuration;
            decisionTimer = Mathf.Max(decisionTimer, hitStunDuration);

            animatorBridge?.ApplyHit(fromDirection);
        }

        public void NotifyDie()
        {
            enabled = false;                  // AI Update 루프 정지
            animatorBridge?.ApplyDie();
        }

        public void NotifyBattleEnded()
        {
            enabled = false;
            _wantsToMove = false;
            currentGuardWalkDirection = GuardWalkDirection.None;
            consecutiveGuardWalkDecisions = 0;
            _engPhase = EngagementPhase.Unaware;

            combatState.currentTacticalMode = AITacticalMode.None;
            combatState.enemy.currentAction = CombatAction.Wait;
            combatState.enemy.currentPhase = CombatPhase.Idle;
            combatState.enemy.phaseElapsedMs = 0f;
            combatState.enemy.isMoving = false;
            combatState.enemy.isGuarding = false;
            combatState.enemy.isParry = false;

            if (animatorBridge != null)
            {
                animatorBridge.ApplyAction(CombatAction.Wait);
                animatorBridge.ApplyPhase(CombatPhase.Idle);
                animatorBridge.ApplyMoving(false);
                animatorBridge.ApplyGuardWalk(false);
                animatorBridge.ApplyParry(false);
                animatorBridge.ApplyInCombat(false);
            }
        }

        public bool CanDealWeaponHit()
        {
            if (!useWeaponHitDetection)
                return false;

            if (combatState == null || combatState.enemy == null)
                return false;

            if (combatState.enemy.hasResolvedThisAction)
                return false;

            if (combatState.enemy.currentPhase != CombatPhase.Active)
                return false;

            return combatState.enemy.currentAction == CombatAction.AttackTopHeavy ||
                   combatState.enemy.currentAction == CombatAction.AttackLeftHeavy ||
                   combatState.enemy.currentAction == CombatAction.AttackRightHeavy;
        }

        public bool TryResolveWeaponHit(EnemyCombatController target)
        {
            if (target == null || target == this)
                return false;

            if (!CanDealWeaponHit())
                return false;

            if (combatState == null || combatState.enemy == null)
                return false;

            if (!ReferenceEquals(combatState.player, target.State.enemy))
                combatState.player = target.State.enemy;

            var result = DefenseApplySystem.ApplyEnemyAttackAgainstPlayer(combatState);
            combatState.enemy.hasResolvedThisAction = true;
            CombatRuntimeLogger.Add(combatState, result);

            if (verboseHitLogging)
                UnityEngine.Debug.Log($"[ResolveHit] {gameObject.name} → {target.name}: {result.ResultType} dir={result.AttackDirection} targetHp={target.CurrentHp}", this);

            switch (result.ResultType)
            {
                case DefenseResultType.Hit:
                {
                    target.NotifyHit(result.AttackDirection);
                    TriggerSlashImpact(target, isParry: false);
                    if (target.CurrentHp <= 0f)
                        target.NotifyDie();
                    break;
                }

                case DefenseResultType.Parry:
                {
                    animatorBridge?.ApplyStunt();
                    TriggerSlashImpact(target, isParry: true);
                    break;
                }
            }

            return result.ResultType != DefenseResultType.None &&
                   result.ResultType != DefenseResultType.Miss;
        }

        void TriggerSlashImpact(EnemyCombatController target, bool isParry)
        {
            if (_swordVFX == null) return;

            // 히트 위치: 공격자 칼끝과 방어자 몸통 중간 지점
            Vector3 hitPoint;
            if (equippedSwordObject != null)
            {
                var tip = equippedSwordObject.transform.Find("Blade_Tip");
                Vector3 tipPos = tip != null ? tip.position : equippedSwordObject.transform.position;
                Vector3 defenderPos = target != null ? target.transform.position + Vector3.up * 1.2f : tipPos;
                hitPoint = (tipPos + defenderPos) * 0.5f;
            }
            else
            {
                hitPoint = transform.position + Vector3.up * 1.2f;
            }

            // 히트 노말: 공격자 → 방어자 방향 역방향
            Vector3 hitNormal = target != null
                ? (transform.position - target.transform.position).normalized
                : -transform.forward;

            if (isParry)
                _swordVFX.OnParryConfirmed(hitPoint, hitNormal);
            else
                _swordVFX.OnHitConfirmed(hitPoint, hitNormal);
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

            ApplyRigidbodyStabilitySettings();

            // 칼의 SwordVFX 참조 확보 (씬에서 직접 부착된 SwordPrefab 인스턴스)
            if (equippedSwordObject != null)
            {
                _swordVFX = equippedSwordObject.GetComponent<SwordVFXController>();
                if (_swordVFX != null && slashProfile != null)
                    _swordVFX.AssignProfile(slashProfile);
            }

            // 자기 칼콜라이더 ↔ 자기 몸콜라이더 충돌 무시 (자해 방지)
            if (bodyCollider != null && swordHitbox != null)
            {
                var swordCol = swordHitbox.GetComponent<Collider>();
                if (swordCol != null) Physics.IgnoreCollision(swordCol, bodyCollider, true);
            }

            ResetDecisionTimer();
        }

        void Update()
        {
            // ── 액션 페이즈 드라이버 ──────────────────────────
            // Hit stun 중에는 phase 진행 / actionElapsed 누적 모두 정지 → animator의 Hit 애니가
            // combatState 보다 먼저 끝나서 다음 공격이 시작되는 race 방지.
            if (!IsHitStunned)
            {
                if (!combatState.useAnimationEventPhaseSync)
                    ActionPhaseDriver.Tick(combatState.enemy, Time.deltaTime);
                else if (combatState.enemy.currentAction != CombatAction.None)
                    combatState.enemy.actionElapsedMs += Time.deltaTime * 1000f;

                TickParryWindow();
            }

            // ── Slash VFX phase 동기화 ────────────────────────
            _swordVFX?.TickPhase(combatState.enemy.currentPhase);

            if (combatState.enableAutoRuntimeResolve && !useWeaponHitDetection)
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
                    currentGuardWalkDirection = GuardWalkDirection.None;
                    consecutiveGuardWalkDecisions = 0;
                    _wantsToMove = false;             // 전투 판단 이동과 분리
                    float distApproach = playerTransform != null
                        ? Vector3.Distance(transform.position, playerTransform.position)
                        : 99f;
                    combatState.distanceMeters = distApproach;
                    combatState.attackRangeMeters = attackRange;
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

                    combatState.distanceMeters = dist;
                    combatState.attackRangeMeters = attackRange;
                    combatState.distanceBucket = dist <= attackRange ? 0 : 1;
                    lastForcedAdvance = ShouldForceAdvance(dist);
                    lastMoveProbability = ComputeMoveProbability(dist);

                    // 페이즈가 Idle로 막 들어온 시각 기록 (post-action dwell 용)
                    var observedPhase = combatState.enemy.currentPhase;
                    if (_lastObservedPhase != CombatPhase.Idle && observedPhase == CombatPhase.Idle)
                        _idleEnteredTime = Time.time;
                    _lastObservedPhase = observedPhase;

                    // 결정 틱: Animator 트랜지션 중이면 보류 (phase 어긋남 방지)
                    // + Idle 진입 후 postActionIdleDwell 시간 이상 지나야 다음 결정 허용
                    // + Hit stun 중에는 결정 자체 차단 (Hit 애니가 ApplyAction으로 덮이지 않게)
                    bool animatorSettled = animatorBridge == null || !animatorBridge.IsInTransition();
                    bool idleDwellOk = (Time.time - _idleEnteredTime) >= postActionIdleDwell;
                    if (combatState.enemy.currentPhase == CombatPhase.Idle && animatorSettled && idleDwellOk && !IsHitStunned)
                    {
                        decisionTimer -= Time.deltaTime;
                        if (decisionTimer <= 0f)
                        {
                            // 이동 중: attackRange 도달 시에만 중지 (경계 진동 방지)
                            // 정지 중: 거리 기반 확률로 이동 여부 결정
                            currentGuardWalkDirection = DecideGuardWalkDirection(dist);
                            _wantsToMove = currentGuardWalkDirection != GuardWalkDirection.None;
                            TickDecision();
                            ResetDecisionTimer();
                        }
                    }
                    else if (combatState.enemy.currentPhase != CombatPhase.Idle)
                    {
                        // 공격/피격/회복 페이즈 중에만 이동 취소
                        // 단순 Animator 트랜지션 중에는 _wantsToMove 유지 (Guard→GuardWalk 깜빡임 방지)
                        _wantsToMove = false;
                        currentGuardWalkDirection = GuardWalkDirection.None;
                        consecutiveGuardWalkDecisions = 0;
                    }

                    isMoving = currentGuardWalkDirection != GuardWalkDirection.None;
                    if (isMoving)
                        MoveGuardWalk(currentGuardWalkDirection);
                    else
                        FaceTowardPlayer();

                    combatState.currentTacticalMode = MapTacticalMode(currentGuardWalkDirection);
                    break;
                }

                // ── 3. Unaware: Idle 대기 ──────────────────────────────────
                default:
                {
                    _wantsToMove = false;
                    currentGuardWalkDirection = GuardWalkDirection.None;
                    consecutiveGuardWalkDecisions = 0;
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
                animatorBridge.ApplyGuardWalk(isInCombat && isMoving);
                animatorBridge.ApplyGuardWalkDirection(currentGuardWalkDirection);
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

        // 회전 속도 (deg/sec). 너무 크면 휙 돌고, 너무 작으면 반응이 늦음.
        [SerializeField] float turnSpeedDeg = 360f;

        void MoveTowardPlayer()
        {
            if (playerTransform == null || rb == null) return;
            if (combatState.enemy.currentPhase != CombatPhase.Idle) return;

            Vector3 dir = (playerTransform.position - transform.position).normalized;
            dir.y = 0f;
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            float stopDistance = attackRange + stopDistanceBuffer;
            if (dist <= stopDistance) return;

            float speed = combatState.difficulty switch
            {
                AIDifficultyType.Easy   => moveSpeed * 0.6f,
                AIDifficultyType.Normal => moveSpeed,
                AIDifficultyType.Hard   => moveSpeed * 1.3f,
                _                       => moveSpeed
            };

            float step = Mathf.Min(speed * Time.deltaTime, (dist - stopDistance) * 0.5f);
            rb.MovePosition(rb.position + dir * step);

            // 플레이어 방향으로 회전 (Rigidbody FreezeRotation 제약 우회 위해 transform 직접 조작)
            ApplyFacing(dir);
        }

        void MoveGuardWalk(GuardWalkDirection direction)
        {
            if (playerTransform == null || rb == null) return;
            if (combatState.enemy.currentPhase != CombatPhase.Idle) return;
            if (direction == GuardWalkDirection.None) return;

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            Vector3 forward = toPlayer.normalized;
            Vector3 moveDir = direction switch
            {
                GuardWalkDirection.Front => forward,
                GuardWalkDirection.Back => -forward,
                GuardWalkDirection.Left => Vector3.Cross(forward, Vector3.up).normalized,
                GuardWalkDirection.Right => Vector3.Cross(Vector3.up, forward).normalized,
                GuardWalkDirection.FrontLeft => (forward + Vector3.Cross(forward, Vector3.up).normalized).normalized,
                GuardWalkDirection.FrontRight => (forward + Vector3.Cross(Vector3.up, forward).normalized).normalized,
                GuardWalkDirection.BackLeft => (-forward + Vector3.Cross(forward, Vector3.up).normalized).normalized,
                GuardWalkDirection.BackRight => (-forward + Vector3.Cross(Vector3.up, forward).normalized).normalized,
                _ => Vector3.zero
            };

            if (moveDir.sqrMagnitude < 0.0001f) return;

            float speedScale = direction switch
            {
                GuardWalkDirection.Back => 0.9f,
                GuardWalkDirection.Left => 0.85f,
                GuardWalkDirection.Right => 0.85f,
                GuardWalkDirection.FrontLeft => 0.9f,
                GuardWalkDirection.FrontRight => 0.9f,
                GuardWalkDirection.BackLeft => 0.8f,
                GuardWalkDirection.BackRight => 0.8f,
                _ => 1f
            };

            float speed = moveSpeed * guardWalkSpeedMultiplier * speedScale;
            float step = speed * Time.deltaTime;
            if (IsForwardGuardWalk(direction))
            {
                float stopDistance = attackRange + stopDistanceBuffer;
                if (toPlayer.magnitude <= stopDistance)
                {
                    FaceTowardPlayer();
                    return;
                }

                step = Mathf.Min(step, (toPlayer.magnitude - stopDistance) * 0.5f);
            }

            rb.MovePosition(rb.position + moveDir * step);
            FaceTowardPlayer();
        }

        static bool IsForwardGuardWalk(GuardWalkDirection direction)
        {
            return direction == GuardWalkDirection.Front
                || direction == GuardWalkDirection.FrontLeft
                || direction == GuardWalkDirection.FrontRight;
        }

        void FaceTowardPlayer()
        {
            if (playerTransform == null) return;

            Vector3 dir = (playerTransform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                ApplyFacing(dir.normalized);
        }

        void ApplyFacing(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(flatDir, Vector3.up);
            Quaternion next   = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeedDeg * Time.deltaTime);

            // Rigidbody.MoveRotation은 FreezeRotation Y 제약이 있으면 무시되므로
            // transform.rotation을 직접 설정. Rigidbody가 있으면 rb.rotation도 동기화.
            transform.rotation = next;
            if (rb != null)
                rb.rotation = next;
        }

        float GetMindGameRange()
        {
            return Mathf.Max(mindGameRange, attackRange + 0.15f);
        }

        GuardWalkDirection DecideGuardWalkDirection(float dist)
        {
            if (_engPhase != EngagementPhase.InCombat)
                return GuardWalkDirection.None;

            // 상대가 Active(공격 휘두르는 중)면 회피 / 가드 우선 → 이동 보류.
            // Recovery 중에는 오히려 접근 챈스이므로 더 이상 차단하지 않음.
            bool opponentIsAttacking = combatState.player.currentPhase == CombatPhase.Active;
            if (combatState.hasFrameAdvantage || opponentIsAttacking)
                return GuardWalkDirection.None;

            if (lastForcedAdvance)
                return GuardWalkDirection.Front;

            // Aggressive 성향은 사거리 밖이면 거의 무조건 접근 (룬지 거리 끝나면 Front).
            if (combatState.personality == AIPersonalityType.Aggressive
                && dist > attackRange + 0.15f)
                return GuardWalkDirection.Front;

            GetGuardWalkBiases(out float strafeChance, out float retreatChance);

            float intentChance = ComputeGuardWalkIntentProbability(dist);
            if (intentChance <= 0f || Random.value > intentChance)
                return GuardWalkDirection.None;

            if (consecutiveGuardWalkDecisions >= 2 && dist <= attackRange + 0.25f)
                return GuardWalkDirection.None;

            if (dist <= retreatDistance)
            {
                float closeRoll = Random.value;
                if (closeRoll < retreatChance)
                    return RandomBackDirection();

                if (closeRoll < retreatChance + strafeChance)
                    return RandomSideDirection(includeForward: false, includeBack: true);

                return GuardWalkDirection.None;
            }

            if (dist <= strafeDistance)
            {
                float midRoll = Random.value;
                if (midRoll < strafeChance)
                    return RandomSideDirection(includeForward: true, includeBack: false);

                if (combatState.personality == AIPersonalityType.Defensive &&
                    midRoll < strafeChance + (retreatChance * 0.5f))
                    return RandomBackDirection();

                if (dist > attackRange + 0.2f && Random.value < lastMoveProbability * 0.35f)
                    return RandomForwardDirection();

                return GuardWalkDirection.None;
            }

            if (dist > attackRange + 0.3f && Random.value < 0.35f)
                return RandomSideDirection(includeForward: true, includeBack: false);

            return dist > attackRange ? RandomForwardDirection() : GuardWalkDirection.None;
        }

        static GuardWalkDirection RandomForwardDirection()
        {
            float roll = Random.value;
            if (roll < 0.2f) return GuardWalkDirection.FrontLeft;
            if (roll < 0.4f) return GuardWalkDirection.FrontRight;
            return GuardWalkDirection.Front;
        }

        static GuardWalkDirection RandomBackDirection()
        {
            float roll = Random.value;
            if (roll < 0.25f) return GuardWalkDirection.BackLeft;
            if (roll < 0.5f) return GuardWalkDirection.BackRight;
            return GuardWalkDirection.Back;
        }

        static GuardWalkDirection RandomSideDirection(bool includeForward, bool includeBack)
        {
            bool left = Random.value < 0.5f;
            if (includeForward && Random.value < 0.35f)
                return left ? GuardWalkDirection.FrontLeft : GuardWalkDirection.FrontRight;

            if (includeBack && Random.value < 0.35f)
                return left ? GuardWalkDirection.BackLeft : GuardWalkDirection.BackRight;

            return left ? GuardWalkDirection.Left : GuardWalkDirection.Right;
        }

        float ComputeGuardWalkIntentProbability(float dist)
        {
            float baseChance;

            if (dist <= attackRange)
            {
                baseChance = combatState.personality switch
                {
                    AIPersonalityType.Aggressive => 0.03f,
                    AIPersonalityType.Defensive => 0.18f,
                    _ => 0.10f
                };
            }
            else if (dist <= retreatDistance)
            {
                baseChance = combatState.personality switch
                {
                    AIPersonalityType.Aggressive => 0.10f,
                    AIPersonalityType.Defensive => 0.38f,
                    _ => 0.20f
                };
            }
            else if (dist <= strafeDistance)
            {
                baseChance = combatState.personality switch
                {
                    AIPersonalityType.Aggressive => 0.16f,
                    AIPersonalityType.Defensive => 0.48f,
                    _ => 0.30f
                };
            }
            else
            {
                baseChance = Mathf.Clamp01(lastMoveProbability * 0.45f);
            }

            if (combatState.personality == AIPersonalityType.Aggressive && dist <= attackRange + 0.15f)
                baseChance *= 0.35f;

            if (consecutiveGuardWalkDecisions > 0)
            {
                float decay = combatState.personality switch
                {
                    AIPersonalityType.Aggressive => 0.3f,
                    AIPersonalityType.Defensive => 0.55f,
                    _ => 0.4f
                };
                baseChance *= Mathf.Pow(decay, consecutiveGuardWalkDecisions);
            }

            return Mathf.Clamp01(baseChance);
        }

        void GetGuardWalkBiases(out float strafeChance, out float retreatChance)
        {
            switch (combatState.personality)
            {
                case AIPersonalityType.Aggressive:
                    strafeChance = aggressiveStrafeChance;
                    retreatChance = aggressiveRetreatChance;
                    break;
                case AIPersonalityType.Defensive:
                    strafeChance = defensiveStrafeChance;
                    retreatChance = defensiveRetreatChance;
                    break;
                default:
                    strafeChance = defaultStrafeChance;
                    retreatChance = defaultRetreatChance;
                    break;
            }
        }

        static AITacticalMode MapTacticalMode(GuardWalkDirection direction)
        {
            return direction switch
            {
                GuardWalkDirection.Front => AITacticalMode.Advance,
                GuardWalkDirection.FrontLeft => AITacticalMode.Advance,
                GuardWalkDirection.FrontRight => AITacticalMode.Advance,
                GuardWalkDirection.Back => AITacticalMode.Retreat,
                GuardWalkDirection.BackLeft => AITacticalMode.Retreat,
                GuardWalkDirection.BackRight => AITacticalMode.Retreat,
                GuardWalkDirection.Left => AITacticalMode.StrafeLeft,
                GuardWalkDirection.Right => AITacticalMode.StrafeRight,
                _ => AITacticalMode.None
            };
        }

        bool ShouldForceAdvance(float dist)
        {
            return dist > GetMindGameRange();
        }

        float ComputeMoveProbability(float dist)
        {
            if (dist <= attackRange)
                return 0f;

            if (ShouldForceAdvance(dist))
                return 1f;

            float playRange = Mathf.Max(GetMindGameRange() - attackRange, 0.01f);
            float t = Mathf.Clamp01((dist - attackRange) / playRange);
            float curved = Mathf.Pow(t, moveCurvePow);
            return Mathf.Clamp(closeRangeMoveBias + ((1f - closeRangeMoveBias) * curved), 0f, 1f);
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
                var action = NormalizeInCombatAction(trace.Action, in observation);

                bool executed = ActionExecutor.TryExecute(combatState, action);
                if (executed)
                {
                    RecordRecentAttack(action);
                    SyncGuardWalkDecisionState(action);
                    ApplyAnimatorDecision(action);
                }
                return action;
            }
            else
            {
                combatState.currentTacticalMode = AITacticalMode.None;
                var action = NormalizeInCombatAction(currentBrain.Decide(in observation), in observation);

                bool executed = ActionExecutor.TryExecute(combatState, action);
                if (executed)
                {
                    RecordRecentAttack(action);
                    SyncGuardWalkDecisionState(action);
                    ApplyAnimatorDecision(action);
                }
                return action;
            }
        }

        CombatAction NormalizeInCombatAction(CombatAction action, in CombatObservation observation)
        {
            lastObservedEnemyDirection = GetObservedEnemyDirection(in observation);

            if (_engPhase != EngagementPhase.InCombat)
                return action;

            if (action == CombatAction.None || action == CombatAction.Wait)
                return GuardActionFromDirection(lastObservedEnemyDirection);

            return action;
        }

        AttackDirection GetObservedEnemyDirection(in CombatObservation observation)
        {
            if (observation.EnemyCurrentDirection != AttackDirection.None)
                return observation.EnemyCurrentDirection;

            if (lastObservedEnemyDirection != AttackDirection.None)
                return lastObservedEnemyDirection;

            return AttackDirection.Top;
        }

        static CombatAction GuardActionFromDirection(AttackDirection direction)
        {
            return direction switch
            {
                AttackDirection.Left => CombatAction.GuardLeft,
                AttackDirection.Right => CombatAction.GuardRight,
                _ => CombatAction.GuardTop
            };
        }

        void RecordRecentAttack(CombatAction action)
        {
            AttackDirection direction = action switch
            {
                CombatAction.AttackTopHeavy => AttackDirection.Top,
                CombatAction.AttackLeftHeavy => AttackDirection.Left,
                CombatAction.AttackRightHeavy => AttackDirection.Right,
                _ => AttackDirection.None
            };

            if (direction == AttackDirection.None)
                return;

            combatState.recentAttackC = combatState.recentAttackB;
            combatState.recentAttackB = combatState.recentAttackA;
            combatState.recentAttackA = direction;
        }

        void SyncGuardWalkDecisionState(CombatAction action)
        {
            if (_engPhase != EngagementPhase.InCombat)
            {
                currentGuardWalkDirection = GuardWalkDirection.None;
                consecutiveGuardWalkDecisions = 0;
                return;
            }

            if (IsAttackAction(action) || IsParryAction(action))
            {
                currentGuardWalkDirection = GuardWalkDirection.None;
                consecutiveGuardWalkDecisions = 0;
                _wantsToMove = false;
                combatState.enemy.isMoving = false;
                combatState.currentTacticalMode = AITacticalMode.None;
                return;
            }

            if (currentGuardWalkDirection != GuardWalkDirection.None)
            {
                consecutiveGuardWalkDecisions++;
                return;
            }

            consecutiveGuardWalkDecisions = 0;
        }

        static bool IsAttackAction(CombatAction action)
        {
            return action == CombatAction.AttackTopHeavy
                || action == CombatAction.AttackLeftHeavy
                || action == CombatAction.AttackRightHeavy;
        }

        static bool IsParryAction(CombatAction action)
        {
            return action == CombatAction.ParryTop
                || action == CombatAction.ParryLeft
                || action == CombatAction.ParryRight;
        }

        void TickParryWindow()
        {
            var fighter = combatState.enemy;
            if (fighter == null || !IsParryAction(fighter.currentAction))
                return;

            float windowMs = Mathf.Max(0.01f, parryWindowDuration) * 1000f;
            float recoveryMs = Mathf.Max(parryRecoveryDuration, parryWindowDuration) * 1000f;

            if (fighter.isParryWindowOpen && fighter.actionElapsedMs >= windowMs)
                fighter.isParryWindowOpen = false;

            if (fighter.isParry && fighter.actionElapsedMs >= recoveryMs)
            {
                fighter.isParry = false;
                fighter.currentAction = GuardActionFromDirection(fighter.currentDirection);
                fighter.currentPhase = CombatPhase.Idle;
                fighter.phaseElapsedMs = 0f;
                fighter.actionElapsedMs = 0f;
            }
        }

        void ApplyAnimatorDecision(CombatAction action)
        {
            if (animatorBridge == null)
                return;

            animatorBridge.ApplyAction(action);
            animatorBridge.ApplyPhase(combatState.enemy.currentPhase);
            animatorBridge.ApplyParry(combatState.enemy.isParry);
            animatorBridge.ApplyGuardWalk(_engPhase == EngagementPhase.InCombat && combatState.enemy.isMoving);
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

        void ApplyRigidbodyStabilitySettings()
        {
            if (rb == null)
                return;

            rb.useGravity = false;
            rb.collisionDetectionMode = characterCollisionDetection;
            rb.interpolation = characterInterpolation;

            var constraints = rb.constraints;

            constraints = freezeVerticalPosition
                ? constraints | RigidbodyConstraints.FreezePositionY
                : constraints & ~RigidbodyConstraints.FreezePositionY;

            if (freezeTiltRotation)
                constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            else
                constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);

            rb.constraints = constraints;
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
            var action = NormalizeInCombatAction(currentBrain.Decide(in observation), in observation);
            decisionWatch.Stop();

            var executeWatch = Stopwatch.StartNew();
            bool executed = ActionExecutor.TryExecute(combatState, action);
            executeWatch.Stop();

            double animatorMs = 0.0;
            bool animatorApplied = false;

            if (executed && animatorBridge != null)
            {
                var animatorWatch = Stopwatch.StartNew();
                RecordRecentAttack(action);
                SyncGuardWalkDecisionState(action);
                ApplyAnimatorDecision(action);
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

        // ─── Debug Snapshot (Editor/플레이 중 디버그 창에서 사용) ────────────
        public struct AIDebugSnapshot
        {
            public string  ObjectName;

            // Engagement
            public string  EngagementPhase;
            public float   DistToTarget;
            public bool    WantsToMove;
            public float   DecisionTimer;
            public int     DistanceBucket;

            // Combat state
            public string  CombatPhase;
            public string  CurrentAction;
            public string  CurrentDirection;
            public string  ObservedEnemyDirection;
            public string  GuardWalkDirection;
            public string  TacticalMode;
            public bool    IsGuarding;
            public bool    IsMoving;
            public float   Hp;
            public float   MoveProbability;
            public bool    ForcedAdvance;
            public float   MindGameRange;

            // Animator
            public bool    AnimatorInTransition;
            public bool    AnimatorSettled;
            public string  AnimatorState;
            public int     AnimActionType;
            public float   AnimDirection;
            public bool    AnimIsMoving;
            public bool    AnimIsInCombat;
        }

        public AIDebugSnapshot GetDebugSnapshot()
        {
            float dist = playerTransform != null
                ? Vector3.Distance(transform.position, playerTransform.position)
                : -1f;

            bool inTransition = animatorBridge != null && animatorBridge.IsInTransition();
            bool settled      = animatorBridge == null || !inTransition;

            return new AIDebugSnapshot
            {
                ObjectName          = gameObject.name,

                EngagementPhase     = _engPhase.ToString(),
                DistToTarget        = dist,
                WantsToMove         = _wantsToMove,
                DecisionTimer       = decisionTimer,
                DistanceBucket      = combatState.distanceBucket,

                CombatPhase         = combatState.enemy.currentPhase.ToString(),
                CurrentAction       = combatState.enemy.currentAction.ToString(),
                CurrentDirection    = combatState.enemy.currentDirection.ToString(),
                ObservedEnemyDirection = lastObservedEnemyDirection.ToString(),
                GuardWalkDirection  = currentGuardWalkDirection.ToString(),
                TacticalMode        = combatState.currentTacticalMode.ToString(),
                IsGuarding          = combatState.enemy.isGuarding,
                IsMoving            = combatState.enemy.isMoving,
                Hp                  = combatState.enemy.hp,
                MoveProbability     = lastMoveProbability,
                ForcedAdvance       = lastForcedAdvance,
                MindGameRange       = GetMindGameRange(),

                AnimatorInTransition = inTransition,
                AnimatorSettled      = settled,
                AnimatorState        = animatorBridge != null ? animatorBridge.GetCurrentStateFullName() : "none",
                AnimActionType       = animatorBridge != null ? animatorBridge.GetActionTypeValue()  : -1,
                AnimDirection        = animatorBridge != null ? animatorBridge.GetDirectionValue()   : -1f,
                AnimIsMoving         = animatorBridge != null && animatorBridge.GetIsMovingValue(),
                AnimIsInCombat       = animatorBridge != null && animatorBridge.GetIsInCombatValue(),
            };
        }

        public string GetActionCandidatesLog()
        {
            EnsureBrainInitialized();
            if (currentBrain == null) return "brain is null";

            if (!(currentBrain is UtilityBrain ub)) return "not a UtilityBrain";

            var obs  = ObservationBuilder.Build(combatState, parryLearner);
            var list = ub.GetCandidates(in obs);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{gameObject.name}] Action Candidates  (dist={obs.DistanceBucket} bucket, hasFrAdv={obs.HasFrameAdvantage})");
            sb.AppendLine($"  readDir={GetObservedEnemyDirection(in obs)}  currentGuard={combatState.enemy.currentDirection}  walkDir={currentGuardWalkDirection}  moveProb={lastMoveProbability:F2}  forceAdvance={lastForcedAdvance}");
            foreach (var c in list.OrderByDescending(x => x.Score))
                sb.AppendLine($"  {c.Action,-24} {c.Score:+0.00;-0.00}");
            return sb.ToString();
        }

        void OnDrawGizmos()
        {
            Vector3 origin = transform.position + Vector3.up * 0.05f;

            Gizmos.color = new Color(1, 0, 0, 1);
            Gizmos.DrawWireSphere(origin, attackRange);

            Gizmos.color = new Color(0, 1, 0, 1);
            Gizmos.DrawWireSphere(origin, GetMindGameRange());

            Gizmos.color = new Color(1, 1, 1, 1);
            Gizmos.DrawWireSphere(origin, combatRange);

            Gizmos.color = new Color(0, 1, 1, 1);
            Gizmos.DrawWireSphere(origin, detectionRange);

            if (playerTransform == null)
                return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            Gizmos.color = dist <= attackRange
                ? new Color(1, 0, 0, 1)
                : dist <= GetMindGameRange()
                    ? new Color(1, 0, 0, 1)
                    : new Color(1, 0, 0, 1);
            Gizmos.DrawLine(origin, playerTransform.position + Vector3.up * 0.05f);
        }
    }
}
