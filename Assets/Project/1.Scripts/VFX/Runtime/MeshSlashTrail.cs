using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 칼의 Blade_Base ~ Blade_Tip 두 Transform을 프레임마다 샘플링해
    /// 리본 메시를 동적으로 생성하는 커스텀 트레일.
    ///
    /// TrailRenderer보다 비용 약간 높지만:
    ///   - 폭/두께/곡률 자유
    ///   - 두 점 리본 → 칼날 형상 그대로 보존
    ///   - 세그먼트별 age를 vertex color.a로 셰이더에 전달
    ///
    /// 비활성 시 기존 세그먼트는 살아있다가 수명만료로 소멸.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class MeshSlashTrail : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform bladeBase;
        [SerializeField] Transform bladeTip;

        [Header("Config")]
        [Range(4, 64)] [SerializeField] int maxSegments = 24;
        [SerializeField] float lifetime = 0.22f;
        [SerializeField] float widthScale = 1f;
        [Tooltip("이 속도(m/s) 이하에서는 샘플링하지 않음")]
        [SerializeField] float minTipSpeed = 2.5f;
        [Tooltip("두 샘플 간 최소 거리 — 진동 방지")]
        [SerializeField] float minSegmentDist = 0.02f;

        [Header("Material")]
        [Tooltip("비워두면 Game/VFX/SlashTrail 셰이더로 런타임 생성")]
        [SerializeField] Material sharedMaterial;
        [Tooltip("비워두면 Game/VFX/SlashDistortion 셰이더로 런타임 생성")]
        [SerializeField] Material distortionMaterial;
        [SerializeField] bool enableDistortion = true;
        [SerializeField] Texture2D noiseTexture;

        [Header("Runtime")]
        [SerializeField] bool isEmitting = false;

        // Shader property IDs
        static readonly int IdCoreColor       = Shader.PropertyToID("_CoreColor");
        static readonly int IdRimColor        = Shader.PropertyToID("_RimColor");
        static readonly int IdEmission        = Shader.PropertyToID("_Emission");
        static readonly int IdAlphaMultiplier = Shader.PropertyToID("_AlphaMultiplier");
        static readonly int IdNoiseTex        = Shader.PropertyToID("_NoiseTex");
        static readonly int IdNoiseScale      = Shader.PropertyToID("_NoiseScale");
        static readonly int IdNoisePanSpeed   = Shader.PropertyToID("_NoisePanSpeed");
        static readonly int IdNoiseStrength   = Shader.PropertyToID("_NoiseStrength");
        static readonly int IdRimPower        = Shader.PropertyToID("_RimPower");
        static readonly int IdCoreWidth       = Shader.PropertyToID("_CoreWidth");
        static readonly int IdFadePower       = Shader.PropertyToID("_FadePower");
        static readonly int IdDistStrength    = Shader.PropertyToID("_DistortionStrength");
        static readonly int IdDistTex         = Shader.PropertyToID("_DistortionTex");

        struct Segment
        {
            public Vector3 a;      // base (near hand)
            public Vector3 b;      // tip
            public float   bornAt; // Time.time
        }

        readonly List<Segment> _segs = new List<Segment>(64);
        Mesh _mesh;
        MeshFilter _mf;
        MeshRenderer _mr;

        Vector3 _lastTipPos;
        float _lastTipTime;
        bool _hasLastTip;

        public void Configure(Transform bladeBaseT, Transform bladeTipT, float lifetimeSec,
                              int segments, float width, float minSpeed)
        {
            bladeBase = bladeBaseT;
            bladeTip = bladeTipT;
            lifetime = lifetimeSec;
            maxSegments = Mathf.Clamp(segments, 4, 64);
            widthScale = width;
            minTipSpeed = minSpeed;
            _segs.Clear();
        }

        public void SetEmitting(bool value) => isEmitting = value;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "SlashTrail (dynamic)" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;

            EnsureMaterial();

            // VFX는 그림자 안 남기고, 라이트 프로브도 필요 없음
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _mr.allowOcclusionWhenDynamic = false;
        }

        void EnsureMaterial()
        {
            var noise = noiseTexture != null ? noiseTexture : ProceduralNoiseTexture.GetSlashNoise();

            if (sharedMaterial == null)
            {
                var sh = Shader.Find("Game/VFX/SlashTrail");
                if (sh == null)
                {
                    Debug.LogError("[MeshSlashTrail] Shader 'Game/VFX/SlashTrail' not found");
                    return;
                }
                sharedMaterial = new Material(sh) { name = "SlashTrail_Runtime" };
                sharedMaterial.SetTexture(IdNoiseTex, noise);
            }

            if (enableDistortion && distortionMaterial == null)
            {
                var sh2 = Shader.Find("Game/VFX/SlashDistortion");
                if (sh2 != null)
                {
                    distortionMaterial = new Material(sh2) { name = "SlashDistortion_Runtime" };
                    distortionMaterial.SetTexture(IdDistTex, noise);
                }
            }

            // 두 머티리얼 동시 할당: 왜곡 먼저, 본체 나중 (Queue로도 분리됨)
            if (enableDistortion && distortionMaterial != null)
                _mr.sharedMaterials = new[] { distortionMaterial, sharedMaterial };
            else
                _mr.sharedMaterial = sharedMaterial;
        }

        /// <summary>SwordVFXController에서 프로파일 적용 시 호출.</summary>
        public void ApplyMaterialParams(Color core, Color rim, float emission, float alphaMul)
        {
            if (sharedMaterial == null) EnsureMaterial();
            if (sharedMaterial == null) return;

            sharedMaterial.SetColor(IdCoreColor, core);
            sharedMaterial.SetColor(IdRimColor,  rim);
            sharedMaterial.SetFloat(IdEmission,        emission);
            sharedMaterial.SetFloat(IdAlphaMultiplier, alphaMul);
        }

        void LateUpdate()
        {
            if (bladeBase == null || bladeTip == null)
            {
                ClearMesh();
                return;
            }

            float now = Time.time;

            // 1) 속도 체크 후 새 세그먼트 추가 (emit 중일 때만)
            if (isEmitting)
                TrySampleSegment(now);

            // 2) 수명 초과 제거
            _segs.RemoveAll(s => now - s.bornAt > lifetime);

            // 3) 메시 재구성
            RebuildMesh(now);
        }

        void TrySampleSegment(float now)
        {
            Vector3 tipPos = bladeTip.position;

            if (_hasLastTip)
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                float speed = (tipPos - _lastTipPos).magnitude / dt;
                if (speed < minTipSpeed)
                {
                    _lastTipPos = tipPos;
                    _lastTipTime = now;
                    return;
                }
            }

            // 중복/진동 방지: 마지막 세그먼트와 거리 체크
            if (_segs.Count > 0)
            {
                var last = _segs[_segs.Count - 1];
                if ((last.b - tipPos).sqrMagnitude < minSegmentDist * minSegmentDist)
                {
                    _lastTipPos = tipPos;
                    _lastTipTime = now;
                    return;
                }
            }

            Vector3 basePos = bladeBase.position;
            // widthScale: base→tip 벡터를 tip 기준으로 스케일
            Vector3 mid = (basePos + tipPos) * 0.5f;
            Vector3 scaledBase = mid + (basePos - mid) * widthScale;
            Vector3 scaledTip  = mid + (tipPos  - mid) * widthScale;

            _segs.Add(new Segment { a = scaledBase, b = scaledTip, bornAt = now });

            // 최대 개수 초과 시 가장 오래된 제거
            if (_segs.Count > maxSegments)
                _segs.RemoveAt(0);

            _lastTipPos = tipPos;
            _lastTipTime = now;
            _hasLastTip = true;
        }

        void RebuildMesh(float now)
        {
            _mesh.Clear();
            int n = _segs.Count;
            if (n < 2)
                return;

            var verts = new Vector3[n * 2];
            var uvs   = new Vector2[n * 2];
            var cols  = new Color[n * 2];
            var tris  = new int[(n - 1) * 6];

            for (int i = 0; i < n; i++)
            {
                var s = _segs[i];
                verts[i * 2 + 0] = s.a;
                verts[i * 2 + 1] = s.b;

                float u = (float)i / (n - 1);     // 진행 방향
                uvs[i * 2 + 0] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);

                float age = Mathf.Clamp01((now - s.bornAt) / lifetime);
                float alpha = 1f - age;   // 선형 페이드, 셰이더에서 커브 적용
                var c = new Color(1f, 1f, 1f, alpha);
                cols[i * 2 + 0] = c;
                cols[i * 2 + 1] = c;
            }

            for (int i = 0; i < n - 1; i++)
            {
                int b = i * 2;
                int t = i * 6;
                tris[t + 0] = b + 0;
                tris[t + 1] = b + 2;
                tris[t + 2] = b + 1;
                tris[t + 3] = b + 1;
                tris[t + 4] = b + 2;
                tris[t + 5] = b + 3;
            }

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.colors = cols;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        void ClearMesh()
        {
            if (_mesh != null) _mesh.Clear();
            _segs.Clear();
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
        }
    }
}
