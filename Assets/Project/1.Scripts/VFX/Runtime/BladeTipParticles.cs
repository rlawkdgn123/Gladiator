using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 칼 끝(Blade_Tip)에 붙어서, 칼이 빠르게 움직일 때만 파티클을 방출.
    /// 세 레이어:
    ///   1) Sparks - 얇은 스트레치 빌보드, 빠르게 소멸, 발광
    ///   2) Dust   - 크고 부드러운 회색, 약한 중력, 오래 남음
    ///   3) Heat   - 얇은 웜톤 스트릭, 공기 가르는 느낌
    ///
    /// 프리팹 없이 런타임에 ParticleSystem 3개 생성.
    /// CombatPhase.Active일 때만 emission rate 올림.
    /// </summary>
    [DisallowMultipleComponent]
    public class BladeTipParticles : MonoBehaviour
    {
        [SerializeField] Transform bladeTip;
        [SerializeField] float minTipSpeed = 2.5f;
        [SerializeField] float emitMultiplierActive = 1f;
        [SerializeField] float emitMultiplierIdle = 0f;

        ParticleSystem _sparks;
        ParticleSystem _dust;
        ParticleSystem _heat;

        Vector3 _lastTipPos;
        bool _hasLast;
        bool _isActive;

        public void Configure(Transform bladeTipT, float minSpeed)
        {
            bladeTip = bladeTipT;
            minTipSpeed = minSpeed;
        }

        public void SetActive(bool active) => _isActive = active;

        void Awake()
        {
            BuildSparks();
            BuildDust();
            BuildHeat();
        }

        void LateUpdate()
        {
            if (bladeTip == null) return;

            Vector3 tipPos = bladeTip.position;
            float speed = 0f;
            if (_hasLast)
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                speed = (tipPos - _lastTipPos).magnitude / dt;
            }
            _lastTipPos = tipPos;
            _hasLast = true;

            // 샘플 위치 추적
            transform.position = tipPos;

            // 속도 기반 emission 배율
            float speedFactor = Mathf.InverseLerp(minTipSpeed, minTipSpeed * 4f, speed);
            float mul = (_isActive ? emitMultiplierActive : emitMultiplierIdle) * speedFactor;

            SetRateMultiplier(_sparks, mul * 1.2f);
            SetRateMultiplier(_dust,   mul * 0.8f);
            SetRateMultiplier(_heat,   mul * 1.0f);
        }

        static void SetRateMultiplier(ParticleSystem ps, float mul)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTimeMultiplier = mul;
        }

        // ────────────────────────────────────────────────────
        ParticleSystem CreateChild(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            return ps;
        }

        void BuildSparks()
        {
            _sparks = CreateChild("Sparks");
            var main = _sparks.main;
            main.startLifetime = 0.35f;
            main.startSize = 0.04f;
            main.startSpeed = 2.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.4f, 1.1f, 0.7f, 1f), new Color(1.2f, 0.9f, 0.4f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.gravityModifier = 0.3f;

            var em = _sparks.emission;
            em.rateOverTime = 60f;
            em.rateOverTimeMultiplier = 0f;

            var sh = _sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.02f;

            var col = _sparks.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.4f, 1.1f, 0.7f), 0f),
                    new GradientColorKey(new Color(1.0f, 0.6f, 0.2f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sz = _sparks.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

            var r = _sparks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3f;
            r.material = CreateAdditiveMaterial();
        }

        void BuildDust()
        {
            _dust = CreateChild("Dust");
            var main = _dust.main;
            main.startLifetime = 0.9f;
            main.startSize = 0.15f;
            main.startSpeed = 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.52f, 0.48f, 0.5f),
                new Color(0.65f, 0.6f, 0.55f, 0.4f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.maxParticles = 120;

            var em = _dust.emission;
            em.rateOverTime = 25f;
            em.rateOverTimeMultiplier = 0f;

            var sh = _dust.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 25f;
            sh.radius = 0.03f;

            var col = _dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 0f),
                        new GradientColorKey(new Color(0.5f, 0.48f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(0.4f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sz = _dust.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.8f));

            var r = _dust.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = CreateAlphaBlendedMaterial();
        }

        void BuildHeat()
        {
            _heat = CreateChild("HeatStreak");
            var main = _heat.main;
            main.startLifetime = 0.25f;
            main.startSize = 0.03f;
            main.startSpeed = 1f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.3f, 0.8f, 0.5f, 0.7f),
                new Color(1.0f, 0.5f, 0.3f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            var em = _heat.emission;
            em.rateOverTime = 30f;
            em.rateOverTimeMultiplier = 0f;

            var sh = _heat.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.01f;

            var col = _heat.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1.3f, 0.8f, 0.5f), 0f),
                        new GradientColorKey(new Color(0.8f, 0.3f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = _heat.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 4f;
            r.material = CreateAdditiveMaterial();
        }

        // ────────────────────────────────────────────────────
        static Material _cachedAdditive;
        static Material _cachedAlpha;

        static Material CreateAdditiveMaterial()
        {
            if (_cachedAdditive != null) return _cachedAdditive;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "BladeTipParticles_Additive" };
            // URP Particles/Unlit: Surface = 1 (Transparent), Blend = 1 (Additive)
            if (m.HasProperty("_Surface"))    m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend"))      m.SetFloat("_Blend", 1f);
            if (m.HasProperty("_SrcBlend"))   m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))   m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite"))     m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3100;
            _cachedAdditive = m;
            return m;
        }

        static Material CreateAlphaBlendedMaterial()
        {
            if (_cachedAlpha != null) return _cachedAlpha;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "BladeTipParticles_Alpha" };
            if (m.HasProperty("_Surface"))    m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend"))      m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend"))   m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))   m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite"))     m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3090;
            _cachedAlpha = m;
            return m;
        }
    }
}
