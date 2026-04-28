using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 타격 지점에 4종 임팩트 레이어를 스폰. 프리팹 없이 런타임 생성.
    ///   1) Flash     - Point Light 짧게 번쩍
    ///   2) Sparks    - 방사형 버스트 (스트레치 빌보드)
    ///   3) Dust      - 먼지 버스트 (소프트 알파)
    ///   4) Shockwave - 확산 링 쿼드 (ShockwaveRing 셰이더)
    /// 수명 후 자동 삭제.
    /// </summary>
    public class ImpactVFX : MonoBehaviour
    {
        public static void Spawn(Vector3 position, Vector3 normal, Color tint,
                                 float lifetime = 0.6f, float scale = 1f)
        {
            var root = new GameObject("ImpactVFX");
            root.transform.position = position;
            if (normal.sqrMagnitude > 1e-4f)
                root.transform.rotation = Quaternion.LookRotation(normal);
            root.transform.localScale = Vector3.one * scale;

            var c = root.AddComponent<ImpactVFX>();
            c.Build(tint, lifetime);
            Destroy(root, lifetime + 0.2f);
        }

        float _lifetime;
        float _elapsed;
        Material _ringMat;
        float _ringStartScale;
        float _ringEndScale;
        Transform _ringT;
        Light _flash;

        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdAlphaMul = Shader.PropertyToID("_AlphaMul");
        static readonly int IdColor    = Shader.PropertyToID("_Color");
        static readonly int IdEmission = Shader.PropertyToID("_Emission");

        void Build(Color tint, float lifetime)
        {
            _lifetime = lifetime;

            BuildFlash(tint);
            BuildSparks(tint);
            BuildDust();
            BuildShockwave(tint);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _lifetime);

            // Flash: 앞 20% 구간에서 강도 5→0
            if (_flash != null)
            {
                float flashT = Mathf.Clamp01(_elapsed / (_lifetime * 0.2f));
                _flash.intensity = Mathf.Lerp(5f, 0f, flashT);
                if (flashT >= 1f) _flash.enabled = false;
            }

            // Shockwave: 시간에 따라 Progress + scale 증가
            if (_ringMat != null && _ringT != null)
            {
                _ringMat.SetFloat(IdProgress, t);
                float s = Mathf.Lerp(_ringStartScale, _ringEndScale, t);
                _ringT.localScale = new Vector3(s, s, s);
                float a = 1f - Mathf.SmoothStep(0.7f, 1f, t);
                _ringMat.SetFloat(IdAlphaMul, a);
            }
        }

        // ────────────────────────────────────────────────────
        void BuildFlash(Color tint)
        {
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(transform, false);
            _flash = lightGo.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = tint;
            _flash.intensity = 5f;
            _flash.range = 3.5f;
            _flash.shadows = LightShadows.None;
        }

        void BuildSparks(Color tint)
        {
            var go = new GameObject("ImpactSparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSize = 0.06f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 80;
            main.startColor = new ParticleSystem.MinMaxGradient(
                tint * 1.3f, tint * 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, 30));

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.05f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(tint * 1.4f, 0f),
                        new GradientColorKey(tint * 0.6f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3f;
            r.material = BladeTipParticlesMaterialAccess.GetAdditive();
        }

        void BuildDust()
        {
            var go = new GameObject("ImpactDust");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = 1.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 50;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.65f, 0.6f, 0.55f, 0.6f),
                new Color(0.55f, 0.52f, 0.48f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, 20));

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.65f, 0.6f, 0.55f), 0f),
                        new GradientColorKey(new Color(0.5f, 0.47f, 0.43f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 2f));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = BladeTipParticlesMaterialAccess.GetAlpha();
        }

        void BuildShockwave(Color tint)
        {
            _ringStartScale = 0.3f;
            _ringEndScale = 2.2f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ImpactShockwave";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            _ringT = go.transform;
            _ringT.localScale = Vector3.one * _ringStartScale;

            var sh = Shader.Find("Game/VFX/ShockwaveRing");
            if (sh == null)
            {
                Debug.LogError("[ImpactVFX] ShockwaveRing shader not found");
                return;
            }
            _ringMat = new Material(sh) { name = "ImpactRing_Runtime" };
            _ringMat.SetColor(IdColor, tint);
            _ringMat.SetFloat(IdEmission, 3f);
            _ringMat.SetFloat(IdProgress, 0f);
            _ringMat.SetFloat(IdAlphaMul, 1f);

            go.GetComponent<MeshRenderer>().sharedMaterial = _ringMat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if (_ringMat != null) Destroy(_ringMat);
        }
    }

    /// <summary>BladeTipParticles 내부에 숨어있는 캐시 머티리얼 재사용 접근자.</summary>
    internal static class BladeTipParticlesMaterialAccess
    {
        static Material _add;
        static Material _alpha;

        public static Material GetAdditive()
        {
            if (_add != null) return _add;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            _add = new Material(sh) { name = "Impact_Additive" };
            if (_add.HasProperty("_Surface")) _add.SetFloat("_Surface", 1f);
            if (_add.HasProperty("_Blend"))   _add.SetFloat("_Blend", 1f);
            if (_add.HasProperty("_SrcBlend")) _add.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_add.HasProperty("_DstBlend")) _add.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (_add.HasProperty("_ZWrite"))  _add.SetFloat("_ZWrite", 0f);
            _add.renderQueue = 3100;
            return _add;
        }

        public static Material GetAlpha()
        {
            if (_alpha != null) return _alpha;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            _alpha = new Material(sh) { name = "Impact_Alpha" };
            if (_alpha.HasProperty("_Surface")) _alpha.SetFloat("_Surface", 1f);
            if (_alpha.HasProperty("_Blend"))   _alpha.SetFloat("_Blend", 0f);
            if (_alpha.HasProperty("_SrcBlend")) _alpha.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_alpha.HasProperty("_DstBlend")) _alpha.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_alpha.HasProperty("_ZWrite"))  _alpha.SetFloat("_ZWrite", 0f);
            _alpha.renderQueue = 3090;
            return _alpha;
        }
    }
}
