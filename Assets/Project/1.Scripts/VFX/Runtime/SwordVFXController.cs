using Game.Core.Enums;
using Game.VFX.Data;
using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 칼(weapon prefab)에 붙어 슬래시 궤적·임팩트·카메라쉐이크를 조율하는 컴포넌트.
    /// EnemyCombatController.ConfigureWeaponObject()에서 자동 추가됨.
    ///
    /// 역할:
    ///  - Blade_Base / Blade_Tip Transform 자동 확보
    ///  - MeshSlashTrail 자식 오브젝트 생성/구성
    ///  - CombatPhase.Active 구간에만 트레일 emit
    ///  - OnHitConfirmed(hitPoint) 호출 시 Impact 스폰 + Hitstop + Impulse
    /// </summary>
    [DisallowMultipleComponent]
    public class SwordVFXController : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] SlashProfileSO profile;

        [Header("Blade Transforms (auto)")]
        [SerializeField] Transform bladeBase;
        [SerializeField] Transform bladeTip;

        [Header("Runtime (read-only)")]
        [SerializeField] bool isEmitting;

        MeshSlashTrail _trail;
        BladeTipParticles _tipParticles;
        CameraShakeImpulse _impulse;

        public SlashProfileSO Profile => profile;

        public void AssignProfile(SlashProfileSO p)
        {
            profile = p;
            ApplyProfileToTrail();
        }

        public void AssignBladeTransforms(Transform baseT, Transform tipT)
        {
            bladeBase = baseT;
            bladeTip = tipT;
            if (_trail != null)
                ApplyProfileToTrail();
        }

        void Awake()
        {
            EnsureTrail();
            EnsureTipParticles();
            EnsureImpulse();
            ApplyProfileToTrail();
        }

        void EnsureTipParticles()
        {
            if (_tipParticles != null) return;

            var go = new GameObject("BladeTipParticles");
            go.transform.SetParent(transform, worldPositionStays: false);
            _tipParticles = go.AddComponent<BladeTipParticles>();
        }

        void EnsureTrail()
        {
            if (_trail != null) return;

            var trailGo = new GameObject("SlashTrail");
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = Vector3.zero;
            trailGo.transform.localRotation = Quaternion.identity;

            _trail = trailGo.AddComponent<MeshSlashTrail>();
            // Phase 2에서 머티리얼/셰이더 연결. 지금은 default 머티리얼.
        }

        void EnsureImpulse()
        {
            _impulse = GetComponent<CameraShakeImpulse>();
            if (_impulse == null)
                _impulse = gameObject.AddComponent<CameraShakeImpulse>();
        }

        void ApplyProfileToTrail()
        {
            if (_trail == null || profile == null) return;
            if (bladeBase == null || bladeTip == null) return;

            _trail.Configure(
                bladeBase, bladeTip,
                profile.trailLifetime,
                profile.trailMaxSegments,
                profile.trailWidthScale,
                profile.minTipSpeed
            );

            _trail.ApplyMaterialParams(
                profile.trailCoreColor,
                profile.trailRimColor,
                profile.trailEmission,
                alphaMul: 1f
            );

            _tipParticles?.Configure(bladeTip, profile.minTipSpeed);
        }

        /// <summary>EnemyCombatController가 매 프레임 CombatPhase 전달.</summary>
        public void TickPhase(CombatPhase phase)
        {
            bool shouldEmit = phase == CombatPhase.Active;
            if (shouldEmit != isEmitting)
            {
                isEmitting = shouldEmit;
                _trail?.SetEmitting(isEmitting);
                _tipParticles?.SetActive(isEmitting);
            }
        }

        /// <summary>타격 확정 시 호출. hitPoint는 월드 좌표.</summary>
        public void OnHitConfirmed(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (profile == null) return;

            // 1) Hitstop
            if (profile.hitstopDuration > 0f)
                Hitstop.Request(profile.hitstopDuration, profile.hitstopScale);

            // 2) Camera shake
            _impulse?.GenerateAt(hitPoint, profile.shakeAmplitude, profile.shakeDuration);

            // 3) Impact prefabs (Phase 4에서 실제 프리팹 스폰)
            SpawnImpactPrefabs(hitPoint, hitNormal);
        }

        /// <summary>패링 성공 시 별도 피드백.</summary>
        public void OnParryConfirmed(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (profile == null) return;

            // 패링은 hitstop 짧게, 셰이크 약하게
            Hitstop.Request(profile.hitstopDuration * 0.6f, profile.hitstopScale);
            _impulse?.GenerateAt(hitPoint, profile.shakeAmplitude * 0.5f, profile.shakeDuration * 0.7f);

            var parryPrefab = profile.parryFlashPrefab != null
                ? profile.parryFlashPrefab
                : profile.impactFlashPrefab;

            if (parryPrefab == null)
            {
                // 패링: 푸른빛, 조금 작게
                var blueTint = new Color(0.7f, 0.9f, 1.25f, 1f);
                ImpactVFX.Spawn(hitPoint, hitNormal, blueTint, lifetime: 0.5f, scale: 0.8f);
            }
            else
            {
                SpawnOneShot(parryPrefab, hitPoint, hitNormal);
            }
        }

        void SpawnImpactPrefabs(Vector3 pos, Vector3 normal)
        {
            bool anyPrefab = profile.impactFlashPrefab    != null
                          || profile.impactSparksPrefab   != null
                          || profile.impactDustPrefab     != null
                          || profile.impactShockwavePrefab != null;

            if (!anyPrefab)
            {
                // 프리팹 없으면 런타임 ImpactVFX로 대체 (검투사 톤)
                var tint = new Color(1.1f, 0.95f, 0.75f, 1f);
                ImpactVFX.Spawn(pos, normal, tint, lifetime: 0.65f, scale: 1f);
                return;
            }

            SpawnOneShot(profile.impactFlashPrefab,     pos, normal);
            SpawnOneShot(profile.impactSparksPrefab,    pos, normal);
            SpawnOneShot(profile.impactDustPrefab,      pos, normal);
            SpawnOneShot(profile.impactShockwavePrefab, pos, normal);
        }

        static void SpawnOneShot(GameObject prefab, Vector3 pos, Vector3 normal)
        {
            if (prefab == null) return;
            var rot = normal.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;
            Instantiate(prefab, pos, rot);
        }
    }
}
