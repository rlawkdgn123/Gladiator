using UnityEngine;

namespace Game.VFX.Data
{
    /// <summary>
    /// 칼 슬래시 VFX 프로파일. 무기/적/난이도별로 에셋 여러 개를 두고 교체.
    /// 검투사(gladius) 톤을 기본값으로 설정.
    /// </summary>
    [CreateAssetMenu(fileName = "SlashProfile_", menuName = "Game/VFX/Slash Profile", order = 0)]
    public class SlashProfileSO : ScriptableObject
    {
        [Header("Trail (MeshTrail)")]
        [Tooltip("궤적 본체 색 (metallic white 권장)")]
        public Color trailCoreColor = new Color(0.95f, 0.96f, 1f, 1f);

        [Tooltip("궤적 테두리 색. 금속 반사 느낌. 살짝 웜 톤")]
        public Color trailRimColor = new Color(1.15f, 1.05f, 0.9f, 1f);

        [Tooltip("궤적 유지 시간 (초)")]
        [Range(0.05f, 1f)] public float trailLifetime = 0.22f;

        [Tooltip("궤적 너비 스케일. 1.0 = 날 길이 그대로")]
        [Range(0.5f, 2f)] public float trailWidthScale = 1f;

        [Tooltip("궤적 emission 강도")]
        [Range(0f, 5f)] public float trailEmission = 1.2f;

        [Tooltip("트레일 메시 최대 세그먼트 (많을수록 부드러움, 비용↑)")]
        [Range(4, 64)] public int trailMaxSegments = 24;

        [Header("Air Distortion")]
        [Tooltip("공기 일렁임 왜곡 강도 (화면 UV 오프셋)")]
        [Range(0f, 0.1f)] public float distortionStrength = 0.018f;

        [Header("Velocity Threshold")]
        [Tooltip("이 속도(m/s) 이상일 때만 궤적 샘플링. 느린 가드 이동에선 궤적 X")]
        public float minTipSpeed = 2.5f;

        [Header("Impact - Hitstop")]
        [Tooltip("타격 시 타임스케일을 줄이는 지속 시간 (초). 0.04~0.06 권장")]
        [Range(0f, 0.2f)] public float hitstopDuration = 0.05f;

        [Tooltip("Hitstop 적용 시의 타임스케일")]
        [Range(0.01f, 1f)] public float hitstopScale = 0.08f;

        [Header("Impact - Camera Shake (Cinemachine 3 Impulse)")]
        [Tooltip("Impulse 강도. 0.2~0.5 권장")]
        public float shakeAmplitude = 0.35f;

        [Tooltip("Impulse 지속 시간")]
        public float shakeDuration = 0.25f;

        [Header("Impact - Prefabs (Phase 4에서 연결)")]
        public GameObject impactFlashPrefab;
        public GameObject impactSparksPrefab;
        public GameObject impactDustPrefab;
        public GameObject impactShockwavePrefab;

        [Header("Parry Feedback (선택)")]
        [Tooltip("패링 성공 시 전용 이펙트 (없으면 impact flash 재사용)")]
        public GameObject parryFlashPrefab;
    }
}
