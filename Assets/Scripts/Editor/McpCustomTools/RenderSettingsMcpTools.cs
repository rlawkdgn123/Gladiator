using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;

namespace Community.Unity.MCP
{
    [McpToolProvider]
    public static class RenderSettingsMcpTools
    {
        // ──────────────────────────────────────────────
        // Args types
        // ──────────────────────────────────────────────

        [Serializable] public class GetRenderSettingsArgs     { /* no args */ }

        [Serializable]
        public class SetRenderSettingsArgs
        {
            [McpParam("Setting name to change. Options: fog_enabled, fog_color, fog_mode, fog_density, fog_start, fog_end, ambient_mode, ambient_sky_color, ambient_equator_color, ambient_ground_color, ambient_intensity, skybox_material, sun_source", Required = true,
                EnumValues = new[] { "fog_enabled", "fog_color", "fog_mode", "fog_density", "fog_start", "fog_end", "ambient_mode", "ambient_sky_color", "ambient_equator_color", "ambient_ground_color", "ambient_intensity", "skybox_material", "sun_source" })]
            public string setting;
            [McpParam("String value (used for: fog_color as '#RRGGBB', fog_mode as 'Linear/Exponential/ExponentialSquared', ambient_mode as 'Skybox/Trilight/Flat/Custom', ambient_sky/equator/ground_color as '#RRGGBB', skybox_material as asset path, sun_source as GameObject name)")]
            public string stringValue;
            [McpParam("Float value (used for: fog_density, fog_start, fog_end, ambient_intensity)")]
            public float floatValue;
            [McpParam("Bool value (used for: fog_enabled)")]
            public bool boolValue;
        }

        [Serializable]
        public class GetPostProcessingArgs
        {
            [McpParam("If true, only return global volumes")]
            public bool globalOnly;
        }

        [Serializable]
        public class SetPostProcessingArgs
        {
            [McpParam("Path to the VolumeProfile asset (use this or volumeName)")]
            public string volumePath;
            [McpParam("Name of the Volume GameObject in scene (use this or volumePath)")]
            public string volumeName;
            [McpParam("Effect name: bloom, color_adjustments, vignette, depth_of_field, chromatic_aberration, motion_blur, film_grain, tonemapping, white_balance", Required = true,
                EnumValues = new[] { "bloom", "color_adjustments", "vignette", "depth_of_field", "chromatic_aberration", "motion_blur", "film_grain", "tonemapping", "white_balance" })]
            public string effect;
            [McpParam("Property name on the effect (e.g. 'intensity', 'threshold', 'scatter', 'postExposure', 'contrast', 'saturation', 'hueShift', 'tint', 'temperature', 'focusDistance', 'aperture', 'focalLength', 'mode')", Required = true)]
            public string property;
            [McpParam("String value for the property")]
            public string stringValue;
            [McpParam("Float value for the property")]
            public float floatValue;
            [McpParam("Bool value for the property (e.g. to enable/disable an override)")]
            public bool boolValue;
            [McpParam("Whether to override this parameter (default true)")]
            public bool setOverride;
        }

        [Serializable]
        public class CreateVolumeArgs
        {
            [McpParam("Name for the new Volume GameObject", Required = true)]
            public string volumeName;
            [McpParam("Path to save the VolumeProfile asset (e.g. Assets/Settings/MyProfile.asset)", Required = true)]
            public string profilePath;
            [McpParam("Whether this is a global volume (default true)")]
            public bool isGlobal;
            [McpParam("Comma-separated list of effects to add (e.g. 'Bloom,Vignette,ColorAdjustments')")]
            public string effects;
        }

        [Serializable]
        public class AddEffectArgs
        {
            [McpParam("Path to the VolumeProfile asset (use this or volumeName)")]
            public string profilePath;
            [McpParam("Name of the Volume GameObject in scene (use this or profilePath)")]
            public string volumeName;
            [McpParam("Effect type to add: Bloom, Vignette, ColorAdjustments, DepthOfField, ChromaticAberration, MotionBlur, FilmGrain, Tonemapping, WhiteBalance", Required = true,
                EnumValues = new[] { "Bloom", "Vignette", "ColorAdjustments", "DepthOfField", "ChromaticAberration", "MotionBlur", "FilmGrain", "Tonemapping", "WhiteBalance" })]
            public string effectType;
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        static bool TryParseColor(string hex, out Color color)
        {
            color = Color.white;
            return ColorUtility.TryParseHtmlString(hex, out color);
        }

        static VolumeProfile ResolveProfile(string profilePath, string volumeName, out string error)
        {
            error = null;
            if (!string.IsNullOrEmpty(profilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (profile == null) { error = $"VolumeProfile not found at '{profilePath}'."; return null; }
                return profile;
            }
            if (!string.IsNullOrEmpty(volumeName))
            {
                var go = GameObject.Find(volumeName);
                if (go == null) { error = $"GameObject '{volumeName}' not found in scene."; return null; }
                var vol = go.GetComponent<Volume>();
                if (vol == null) { error = $"Volume component not found on '{volumeName}'."; return null; }
                if (vol.profile == null) { error = $"Volume '{volumeName}' has no profile assigned."; return null; }
                return vol.profile;
            }
            error = "Either profilePath or volumeName is required.";
            return null;
        }

        static VolumeComponent AddOrGetEffect(VolumeProfile profile, string effectTypeName, out string error)
        {
            error = null;
            switch (effectTypeName.ToLower())
            {
                case "bloom":               return GetOrAdd<Bloom>(profile);
                case "vignette":            return GetOrAdd<Vignette>(profile);
                case "coloradjustments":    return GetOrAdd<ColorAdjustments>(profile);
                case "depthoffield":        return GetOrAdd<DepthOfField>(profile);
                case "chromaticaberration": return GetOrAdd<ChromaticAberration>(profile);
                case "motionblur":          return GetOrAdd<MotionBlur>(profile);
                case "filmgrain":           return GetOrAdd<FilmGrain>(profile);
                case "tonemapping":         return GetOrAdd<Tonemapping>(profile);
                case "whitebalance":        return GetOrAdd<WhiteBalance>(profile);
                default:
                    error = $"Unknown effect type '{effectTypeName}'.";
                    return null;
            }
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var comp))
                comp = profile.Add<T>(true);
            return comp;
        }

        // ──────────────────────────────────────────────
        // Tools
        // ──────────────────────────────────────────────

        [McpTool("unity_get_render_settings", "Returns the current scene render settings (fog, ambient, skybox, sun, etc.).", typeof(GetRenderSettingsArgs))]
        public static object GetRenderSettings(string argsJson)
        {
            var fogColor   = RenderSettings.fogColor;
            var ambSky     = RenderSettings.ambientSkyColor;
            var ambEq      = RenderSettings.ambientEquatorColor;
            var ambGnd     = RenderSettings.ambientGroundColor;
            string skyboxPath = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : null;
            string sunName    = RenderSettings.sun    != null ? RenderSettings.sun.name : null;

            return new
            {
                success = true,
                fog = new
                {
                    enabled = RenderSettings.fog,
                    color   = "#" + ColorUtility.ToHtmlStringRGB(fogColor),
                    mode    = RenderSettings.fogMode.ToString(),
                    density = RenderSettings.fogDensity,
                    startDistance = RenderSettings.fogStartDistance,
                    endDistance   = RenderSettings.fogEndDistance
                },
                ambient = new
                {
                    mode            = RenderSettings.ambientMode.ToString(),
                    skyColor        = "#" + ColorUtility.ToHtmlStringRGB(ambSky),
                    equatorColor    = "#" + ColorUtility.ToHtmlStringRGB(ambEq),
                    groundColor     = "#" + ColorUtility.ToHtmlStringRGB(ambGnd),
                    intensity       = RenderSettings.ambientIntensity
                },
                skyboxMaterial = skyboxPath,
                sunSource      = sunName
            };
        }

        [McpTool("unity_set_render_settings", "Changes a specific render setting in the current scene.", typeof(SetRenderSettingsArgs))]
        public static object SetRenderSettings(string argsJson)
        {
            var args = JsonUtility.FromJson<SetRenderSettingsArgs>(argsJson);
            if (string.IsNullOrEmpty(args.setting)) return new { error = "setting is required." };

            switch (args.setting.ToLower())
            {
                case "fog_enabled":
                    RenderSettings.fog = args.boolValue;
                    break;

                case "fog_color":
                    if (!TryParseColor(args.stringValue, out var fogCol))
                        return new { error = $"Invalid color value '{args.stringValue}'. Use '#RRGGBB' or '#RRGGBBAA'." };
                    RenderSettings.fogColor = fogCol;
                    break;

                case "fog_mode":
                    if (!Enum.TryParse<FogMode>(args.stringValue, true, out var fogMode))
                        return new { error = $"Invalid fog mode '{args.stringValue}'. Use Linear, Exponential or ExponentialSquared." };
                    RenderSettings.fogMode = fogMode;
                    break;

                case "fog_density":
                    RenderSettings.fogDensity = args.floatValue;
                    break;

                case "fog_start":
                    RenderSettings.fogStartDistance = args.floatValue;
                    break;

                case "fog_end":
                    RenderSettings.fogEndDistance = args.floatValue;
                    break;

                case "ambient_mode":
                    if (!Enum.TryParse<AmbientMode>(args.stringValue, true, out var ambMode))
                        return new { error = $"Invalid ambient mode '{args.stringValue}'. Use Skybox, Trilight, Flat or Custom." };
                    RenderSettings.ambientMode = ambMode;
                    break;

                case "ambient_sky_color":
                    if (!TryParseColor(args.stringValue, out var ambSky))
                        return new { error = $"Invalid color '{args.stringValue}'." };
                    RenderSettings.ambientSkyColor = ambSky;
                    break;

                case "ambient_equator_color":
                    if (!TryParseColor(args.stringValue, out var ambEq))
                        return new { error = $"Invalid color '{args.stringValue}'." };
                    RenderSettings.ambientEquatorColor = ambEq;
                    break;

                case "ambient_ground_color":
                    if (!TryParseColor(args.stringValue, out var ambGnd))
                        return new { error = $"Invalid color '{args.stringValue}'." };
                    RenderSettings.ambientGroundColor = ambGnd;
                    break;

                case "ambient_intensity":
                    RenderSettings.ambientIntensity = args.floatValue;
                    break;

                case "skybox_material":
                    var skyMat = AssetDatabase.LoadAssetAtPath<Material>(args.stringValue);
                    if (skyMat == null)
                        return new { error = $"Material not found at '{args.stringValue}'." };
                    RenderSettings.skybox = skyMat;
                    break;

                case "sun_source":
                    var sunGo = GameObject.Find(args.stringValue);
                    if (sunGo == null)
                        return new { error = $"GameObject '{args.stringValue}' not found in scene." };
                    var light = sunGo.GetComponent<Light>();
                    if (light == null)
                        return new { error = $"Light component not found on '{args.stringValue}'." };
                    RenderSettings.sun = light;
                    break;

                default:
                    return new { error = $"Unknown setting '{args.setting}'." };
            }

            EditorUtility.SetDirty(RenderSettings.skybox != null ? (UnityEngine.Object)RenderSettings.skybox : Camera.main);
            return new { success = true, setting = args.setting };
        }

        [McpTool("unity_get_post_processing", "Lists all Volume components in the scene and their effects.", typeof(GetPostProcessingArgs))]
        public static object GetPostProcessing(string argsJson)
        {
            var args = JsonUtility.FromJson<GetPostProcessingArgs>(argsJson);
            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);

            var volList = new List<object>();
            foreach (var vol in volumes)
            {
                if (args.globalOnly && !vol.isGlobal) continue;

                string profilePath = vol.profile != null ? AssetDatabase.GetAssetPath(vol.profile) : null;
                var effectList = new List<string>();
                if (vol.profile != null)
                {
                    foreach (var comp in vol.profile.components)
                        effectList.Add(comp.GetType().Name);
                }

                volList.Add(new
                {
                    gameObjectName = vol.gameObject.name,
                    isGlobal       = vol.isGlobal,
                    priority       = vol.priority,
                    profilePath,
                    effects        = effectList
                });
            }

            return new { success = true, count = volList.Count, volumes = volList };
        }

        [McpTool("unity_set_post_processing", "Changes a property of a post-processing effect inside a VolumeProfile.", typeof(SetPostProcessingArgs))]
        public static object SetPostProcessing(string argsJson)
        {
            var args = JsonUtility.FromJson<SetPostProcessingArgs>(argsJson);
            if (string.IsNullOrEmpty(args.effect))   return new { error = "effect is required." };
            if (string.IsNullOrEmpty(args.property)) return new { error = "property is required." };

            var profile = ResolveProfile(args.volumePath, args.volumeName, out string resolveErr);
            if (profile == null) return new { error = resolveErr };

            bool setOverride = args.setOverride || true; // default to true unless explicitly false

            switch (args.effect.ToLower())
            {
                case "bloom":
                {
                    if (!profile.TryGet<Bloom>(out var bloom)) return new { error = "Bloom not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "intensity":  bloom.intensity.overrideState  = setOverride; bloom.intensity.value  = args.floatValue; break;
                        case "threshold":  bloom.threshold.overrideState  = setOverride; bloom.threshold.value  = args.floatValue; break;
                        case "scatter":    bloom.scatter.overrideState    = setOverride; bloom.scatter.value    = args.floatValue; break;
                        case "clamp":      bloom.clamp.overrideState      = setOverride; bloom.clamp.value      = args.floatValue; break;
                        default: return new { error = $"Unknown Bloom property '{args.property}'." };
                    }
                    break;
                }
                case "color_adjustments":
                {
                    if (!profile.TryGet<ColorAdjustments>(out var ca)) return new { error = "ColorAdjustments not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "postexposure": ca.postExposure.overrideState = setOverride; ca.postExposure.value = args.floatValue; break;
                        case "contrast":     ca.contrast.overrideState     = setOverride; ca.contrast.value     = args.floatValue; break;
                        case "saturation":   ca.saturation.overrideState   = setOverride; ca.saturation.value   = args.floatValue; break;
                        case "hueshif":
                        case "hueshift":     ca.hueShift.overrideState     = setOverride; ca.hueShift.value     = args.floatValue; break;
                        case "tint":
                            if (!TryParseColor(args.stringValue, out var tintCol)) return new { error = $"Invalid tint color '{args.stringValue}'." };
                            ca.colorFilter.overrideState = setOverride; ca.colorFilter.value = tintCol;
                            break;
                        default: return new { error = $"Unknown ColorAdjustments property '{args.property}'." };
                    }
                    break;
                }
                case "vignette":
                {
                    if (!profile.TryGet<Vignette>(out var vig)) return new { error = "Vignette not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "intensity":  vig.intensity.overrideState  = setOverride; vig.intensity.value  = args.floatValue; break;
                        case "smoothness": vig.smoothness.overrideState = setOverride; vig.smoothness.value = args.floatValue; break;
                        case "rounded":    vig.rounded.overrideState    = setOverride; vig.rounded.value    = args.boolValue;  break;
                        case "color":
                            if (!TryParseColor(args.stringValue, out var vigCol)) return new { error = $"Invalid color '{args.stringValue}'." };
                            vig.color.overrideState = setOverride; vig.color.value = vigCol;
                            break;
                        default: return new { error = $"Unknown Vignette property '{args.property}'." };
                    }
                    break;
                }
                case "depth_of_field":
                {
                    if (!profile.TryGet<DepthOfField>(out var dof)) return new { error = "DepthOfField not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "focusdistance": dof.focusDistance.overrideState = setOverride; dof.focusDistance.value = args.floatValue; break;
                        case "aperture":      dof.aperture.overrideState      = setOverride; dof.aperture.value      = args.floatValue; break;
                        case "focallength":   dof.focalLength.overrideState   = setOverride; dof.focalLength.value   = args.floatValue; break;
                        case "mode":
                            if (!Enum.TryParse<DepthOfFieldMode>(args.stringValue, true, out var dofMode))
                                return new { error = $"Invalid DepthOfFieldMode '{args.stringValue}'." };
                            dof.mode.overrideState = setOverride; dof.mode.value = dofMode;
                            break;
                        default: return new { error = $"Unknown DepthOfField property '{args.property}'." };
                    }
                    break;
                }
                case "chromatic_aberration":
                {
                    if (!profile.TryGet<ChromaticAberration>(out var ca)) return new { error = "ChromaticAberration not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "intensity": ca.intensity.overrideState = setOverride; ca.intensity.value = args.floatValue; break;
                        default: return new { error = $"Unknown ChromaticAberration property '{args.property}'." };
                    }
                    break;
                }
                case "motion_blur":
                {
                    if (!profile.TryGet<MotionBlur>(out var mb)) return new { error = "MotionBlur not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "intensity":  mb.intensity.overrideState  = setOverride; mb.intensity.value  = args.floatValue; break;
                        case "clampvalue": mb.clampValue.overrideState = setOverride; mb.clampValue.value = args.floatValue; break;
                        default: return new { error = $"Unknown MotionBlur property '{args.property}'." };
                    }
                    break;
                }
                case "film_grain":
                {
                    if (!profile.TryGet<FilmGrain>(out var fg)) return new { error = "FilmGrain not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "intensity": fg.intensity.overrideState = setOverride; fg.intensity.value = args.floatValue; break;
                        case "response":  fg.response.overrideState  = setOverride; fg.response.value  = args.floatValue; break;
                        default: return new { error = $"Unknown FilmGrain property '{args.property}'." };
                    }
                    break;
                }
                case "tonemapping":
                {
                    if (!profile.TryGet<Tonemapping>(out var tm)) return new { error = "Tonemapping not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "mode":
                            if (!Enum.TryParse<TonemappingMode>(args.stringValue, true, out var tmMode))
                                return new { error = $"Invalid TonemappingMode '{args.stringValue}'." };
                            tm.mode.overrideState = setOverride; tm.mode.value = tmMode;
                            break;
                        default: return new { error = $"Unknown Tonemapping property '{args.property}'." };
                    }
                    break;
                }
                case "white_balance":
                {
                    if (!profile.TryGet<WhiteBalance>(out var wb)) return new { error = "WhiteBalance not found in profile. Add it first." };
                    switch (args.property.ToLower())
                    {
                        case "temperature": wb.temperature.overrideState = setOverride; wb.temperature.value = args.floatValue; break;
                        case "tint":        wb.tint.overrideState        = setOverride; wb.tint.value        = args.floatValue; break;
                        default: return new { error = $"Unknown WhiteBalance property '{args.property}'." };
                    }
                    break;
                }
                default:
                    return new { error = $"Unknown effect '{args.effect}'." };
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return new { success = true, effect = args.effect, property = args.property };
        }

        [McpTool("unity_create_post_processing_volume", "Creates a new Volume GameObject with a VolumeProfile asset and optional effects.", typeof(CreateVolumeArgs))]
        public static object CreatePostProcessingVolume(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateVolumeArgs>(argsJson);
            if (string.IsNullOrEmpty(args.volumeName))  return new { error = "volumeName is required." };
            if (string.IsNullOrEmpty(args.profilePath)) return new { error = "profilePath is required." };

            // Create profile asset
            string dir = System.IO.Path.GetDirectoryName(args.profilePath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, args.profilePath);

            // Add requested effects
            var addedEffects = new List<string>();
            if (!string.IsNullOrEmpty(args.effects))
            {
                foreach (var eff in args.effects.Split(','))
                {
                    string effTrimmed = eff.Trim();
                    var comp = AddOrGetEffect(profile, effTrimmed, out string effErr);
                    if (comp != null)
                        addedEffects.Add(effTrimmed);
                }
            }

            AssetDatabase.SaveAssets();

            // Create GameObject
            var go = new GameObject(args.volumeName);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal  = args.isGlobal || true; // default global
            vol.profile   = profile;

            Undo.RegisterCreatedObjectUndo(go, $"Create Volume {args.volumeName}");
            EditorUtility.SetDirty(go);

            return new
            {
                success = true,
                gameObjectName = go.name,
                profilePath = args.profilePath,
                isGlobal = vol.isGlobal,
                effectsAdded = addedEffects
            };
        }

        [McpTool("unity_add_post_processing_effect", "Adds a VolumeComponent effect to an existing VolumeProfile.", typeof(AddEffectArgs))]
        public static object AddPostProcessingEffect(string argsJson)
        {
            var args = JsonUtility.FromJson<AddEffectArgs>(argsJson);
            if (string.IsNullOrEmpty(args.effectType)) return new { error = "effectType is required." };

            var profile = ResolveProfile(args.profilePath, args.volumeName, out string resolveErr);
            if (profile == null) return new { error = resolveErr };

            var comp = AddOrGetEffect(profile, args.effectType, out string effErr);
            if (comp == null) return new { error = effErr };

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return new { success = true, effectType = args.effectType, componentType = comp.GetType().Name };
        }
    }
}
