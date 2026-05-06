using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.VFX.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EnvironmentRenderingTuner : MonoBehaviour
    {
        const string TerrainHeightBlendKeyword = "_TERRAIN_BLEND_HEIGHT";

        [Header("Terrain Hex")]
        [SerializeField] Terrain[] terrains;
        [SerializeField] Material terrainMaterial;
        [SerializeField] bool enableHeightBlend = true;
        [Range(0f, 1f)] [SerializeField] float heightTransition = 0.35f;
        [Range(0.05f, 4f)] [SerializeField] float hexScale = 0.5f;
        [Range(1f, 12f)] [SerializeField] float hexContrast = 1.2f;
        [Range(0f, 1f)] [SerializeField] float hexRotationStrength = 0.25f;

        [Header("Volume Profile")]
        [SerializeField] Volume realisticVolume;
        [SerializeField] VolumeProfile volumeProfile;

        [Header("Bloom")]
        [Range(0f, 4f)] [SerializeField] float bloomThreshold = 1.25f;
        [Range(0f, 2f)] [SerializeField] float bloomIntensity = 0.16f;
        [Range(0f, 1f)] [SerializeField] float bloomScatter = 0.58f;

        [Header("Color Adjustments")]
        [Range(-2f, 2f)] [SerializeField] float postExposure = 0.05f;
        [Range(-100f, 100f)] [SerializeField] float contrast = 18f;
        [SerializeField] Color colorFilter = new Color(1.03f, 1.01f, 0.97f, 1f);
        [Range(-100f, 100f)] [SerializeField] float saturation = 4f;

        [Header("White Balance")]
        [Range(-100f, 100f)] [SerializeField] float temperature = 8f;
        [Range(-100f, 100f)] [SerializeField] float tint = -2f;

        [Header("Vignette")]
        [Range(0f, 1f)] [SerializeField] float vignetteIntensity = 0.16f;
        [Range(0.01f, 1f)] [SerializeField] float vignetteSmoothness = 0.55f;

        [Header("Lens and Film")]
        [SerializeField] bool depthOfFieldActive = false;
        [Range(0f, 1f)] [SerializeField] float filmGrainIntensity = 0.06f;
        [Range(0f, 1f)] [SerializeField] float filmGrainResponse = 0.85f;
        [Range(0f, 1f)] [SerializeField] float chromaticAberration = 0.018f;

        [Header("Shadows / Midtones / Highlights")]
        [SerializeField] Vector4 shadows = new Vector4(0.94f, 0.96f, 1f, -0.04f);
        [SerializeField] Vector4 midtones = new Vector4(1.015f, 1.005f, 0.98f, 0.015f);
        [SerializeField] Vector4 highlights = new Vector4(1.04f, 1.02f, 0.96f, -0.02f);

        [Header("Screen Top Light")]
        [SerializeField] ScreenTopLightShaftOverlay screenTopLight;
        [SerializeField] Color screenLightColor = new Color(1f, 0.82f, 0.52f, 1f);
        [Range(0f, 1f)] [SerializeField] float screenLightIntensity = 0.28f;
        [Range(0.05f, 1f)] [SerializeField] float screenLightVerticalReach = 0.34f;
        [Range(0.05f, 1f)] [SerializeField] float screenLightHorizontalSpread = 0.82f;
        [Range(0.1f, 3f)] [SerializeField] float screenLightFalloff = 1.55f;
        [SerializeField] Vector2 screenLightSource = new Vector2(0.52f, 0.95f);
        [Range(-0.6f, 0.6f)] [SerializeField] float screenLightTilt = -0.08f;
        [Range(0.02f, 0.35f)] [SerializeField] float screenLightShaftWidth = 0.13f;
        [Range(0f, 1f)] [SerializeField] float screenLightTopGlow = 0.38f;

        void Reset()
        {
            realisticVolume = GetComponent<Volume>();
            if (realisticVolume != null)
                volumeProfile = realisticVolume.sharedProfile;
        }

        void OnEnable()
        {
            ApplyAll();
        }

        void OnValidate()
        {
            ApplyAll();
        }

        [ContextMenu("Apply Environment Rendering Settings")]
        public void ApplyAll()
        {
            ApplyTerrain();
            ApplyVolume();
            ApplyScreenLight();
        }

        void ApplyTerrain()
        {
            if (terrainMaterial == null)
                return;

            if (enableHeightBlend)
                terrainMaterial.EnableKeyword(TerrainHeightBlendKeyword);
            else
                terrainMaterial.DisableKeyword(TerrainHeightBlendKeyword);

            terrainMaterial.SetFloat("_EnableHeightBlend", enableHeightBlend ? 1f : 0f);
            terrainMaterial.SetFloat("_HeightTransition", heightTransition);
            terrainMaterial.SetFloat("_HexScale", hexScale);
            terrainMaterial.SetFloat("_HexContrast", hexContrast);
            terrainMaterial.SetFloat("_HexRotationStrength", hexRotationStrength);

            if (terrains != null)
            {
                foreach (Terrain terrain in terrains)
                {
                    if (terrain != null)
                        terrain.materialTemplate = terrainMaterial;
                }
            }

            MarkDirty(terrainMaterial);
        }

        void ApplyVolume()
        {
            VolumeProfile profile = volumeProfile;
            if (profile == null && realisticVolume != null)
                profile = realisticVolume.sharedProfile;
            if (profile == null)
                return;

            if (profile.TryGet(out Bloom bloom))
            {
                Set(bloom.threshold, bloomThreshold);
                Set(bloom.intensity, bloomIntensity);
                Set(bloom.scatter, bloomScatter);
            }

            if (profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                Set(colorAdjustments.postExposure, postExposure);
                Set(colorAdjustments.contrast, contrast);
                Set(colorAdjustments.colorFilter, colorFilter);
                Set(colorAdjustments.saturation, saturation);
            }

            if (profile.TryGet(out WhiteBalance whiteBalance))
            {
                Set(whiteBalance.temperature, temperature);
                Set(whiteBalance.tint, tint);
            }

            if (profile.TryGet(out Vignette vignette))
            {
                Set(vignette.intensity, vignetteIntensity);
                Set(vignette.smoothness, vignetteSmoothness);
            }

            if (profile.TryGet(out DepthOfField depthOfField))
                depthOfField.active = depthOfFieldActive;

            if (profile.TryGet(out FilmGrain filmGrain))
            {
                Set(filmGrain.intensity, filmGrainIntensity);
                Set(filmGrain.response, filmGrainResponse);
            }

            if (profile.TryGet(out ChromaticAberration chromatic))
                Set(chromatic.intensity, chromaticAberration);

            if (profile.TryGet(out ShadowsMidtonesHighlights smh))
            {
                Set(smh.shadows, shadows);
                Set(smh.midtones, midtones);
                Set(smh.highlights, highlights);
            }

            MarkDirty(profile);
        }

        void ApplyScreenLight()
        {
            if (screenTopLight == null)
                return;

            screenTopLight.ApplySettings(
                screenLightColor,
                screenLightIntensity,
                screenLightVerticalReach,
                screenLightHorizontalSpread,
                screenLightFalloff,
                screenLightSource,
                screenLightTilt,
                screenLightShaftWidth,
                screenLightTopGlow);

            MarkDirty(screenTopLight);
        }

        static void Set(FloatParameter parameter, float value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        static void Set(ColorParameter parameter, Color value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        static void Set(Vector4Parameter parameter, Vector4 value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        static void MarkDirty(Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && target != null)
                EditorUtility.SetDirty(target);
#endif
        }
    }
}
