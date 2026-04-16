using UnityCliConnector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  Render Settings & Post Processing CLI Tools  (URP 17, Unity 6)
//  POST http://localhost:8090/command  { "command": "<name>", "params": {...} }
// ─────────────────────────────────────────────────────────────────────────────

[UnityCliTool(Name = "render_get", Description = "현재 씬의 Render Settings 전체 조회 (fog, ambient, skybox, sun)", Group = "render")]
public static class RenderGetTool
{
    public static object HandleCommand(JObject parameters)
    {
        return new SuccessResponse("OK", new
        {
            fog = new
            {
                enabled  = RenderSettings.fog,
                color    = "#" + ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor),
                mode     = RenderSettings.fogMode.ToString(),
                density  = RenderSettings.fogDensity,
                startDistance = RenderSettings.fogStartDistance,
                endDistance   = RenderSettings.fogEndDistance
            },
            ambient = new
            {
                mode         = RenderSettings.ambientMode.ToString(),
                skyColor     = "#" + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor),
                equatorColor = "#" + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor),
                groundColor  = "#" + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor),
                intensity    = RenderSettings.ambientIntensity
            },
            skyboxMaterial = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : null,
            sunSource      = RenderSettings.sun?.name
        });
    }
}

[UnityCliTool(Name = "render_set", Description = "Render Setting 한 항목 변경. setting: fog_enabled/fog_color/fog_mode/fog_density/fog_start/fog_end/ambient_mode/ambient_sky_color/ambient_equator_color/ambient_ground_color/ambient_intensity/skybox_material/sun_source", Group = "render")]
public static class RenderSetTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p       = new ToolParams(parameters);
        var setting = p.Get("setting");
        if (string.IsNullOrEmpty(setting)) return new ErrorResponse("'setting' required");

        var strVal   = p.Get("string_value", "");
        float fltVal = p.GetFloat("float_value", 0f) ?? 0f;
        bool  boolVal = p.GetBool("bool_value");

        switch (setting.ToLower())
        {
            case "fog_enabled":  RenderSettings.fog = boolVal; break;
            case "fog_density":  RenderSettings.fogDensity = fltVal; break;
            case "fog_start":    RenderSettings.fogStartDistance = fltVal; break;
            case "fog_end":      RenderSettings.fogEndDistance = fltVal; break;
            case "ambient_intensity": RenderSettings.ambientIntensity = fltVal; break;
            case "fog_color":
                if (!ColorUtility.TryParseHtmlString(strVal, out var fogCol)) return new ErrorResponse($"Invalid color '{strVal}'. Use #RRGGBB");
                RenderSettings.fogColor = fogCol; break;
            case "fog_mode":
                if (!Enum.TryParse<FogMode>(strVal, true, out var fogMode)) return new ErrorResponse($"Invalid fog mode. Use Linear/Exponential/ExponentialSquared");
                RenderSettings.fogMode = fogMode; break;
            case "ambient_mode":
                if (!Enum.TryParse<AmbientMode>(strVal, true, out var ambMode)) return new ErrorResponse($"Invalid ambient mode. Use Skybox/Trilight/Flat/Custom");
                RenderSettings.ambientMode = ambMode; break;
            case "ambient_sky_color":
                if (!ColorUtility.TryParseHtmlString(strVal, out var ambSky)) return new ErrorResponse($"Invalid color '{strVal}'");
                RenderSettings.ambientSkyColor = ambSky; break;
            case "ambient_equator_color":
                if (!ColorUtility.TryParseHtmlString(strVal, out var ambEq)) return new ErrorResponse($"Invalid color '{strVal}'");
                RenderSettings.ambientEquatorColor = ambEq; break;
            case "ambient_ground_color":
                if (!ColorUtility.TryParseHtmlString(strVal, out var ambGnd)) return new ErrorResponse($"Invalid color '{strVal}'");
                RenderSettings.ambientGroundColor = ambGnd; break;
            case "skybox_material":
                var mat = AssetDatabase.LoadAssetAtPath<Material>(strVal);
                if (mat == null) return new ErrorResponse($"Material not found: '{strVal}'");
                RenderSettings.skybox = mat; break;
            case "sun_source":
                var go = GameObject.Find(strVal);
                if (go == null) return new ErrorResponse($"GameObject '{strVal}' not found");
                var light = go.GetComponent<Light>();
                if (light == null) return new ErrorResponse($"Light component not found on '{strVal}'");
                RenderSettings.sun = light; break;
            default:
                return new ErrorResponse($"Unknown setting '{setting}'");
        }

        return new SuccessResponse("Render setting updated", new { setting });
    }
}

[UnityCliTool(Name = "pp_get", Description = "씬의 모든 Volume + 이펙트 목록 조회. global_only: true/false", Group = "render")]
public static class PPGetTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p          = new ToolParams(parameters);
        bool globalOnly = p.GetBool("global_only", false);

        var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        var volList = new List<object>();
        foreach (var vol in volumes)
        {
            if (globalOnly && !vol.isGlobal) continue;
            var effects = new List<string>();
            if (vol.profile != null)
                foreach (var comp in vol.profile.components)
                    effects.Add(comp.GetType().Name);

            volList.Add(new
            {
                name        = vol.gameObject.name,
                isGlobal    = vol.isGlobal,
                priority    = vol.priority,
                profilePath = vol.profile != null ? AssetDatabase.GetAssetPath(vol.profile) : null,
                effects
            });
        }
        return new SuccessResponse("OK", new { count = volList.Count, volumes = volList });
    }
}

[UnityCliTool(Name = "pp_create_volume", Description = "Volume GameObject + VolumeProfile 생성. effects: 'Bloom,Vignette,ColorAdjustments,...'", Group = "render")]
public static class PPCreateVolumeTool
{
    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var c)) c = profile.Add<T>(true);
        return c;
    }

    public static object HandleCommand(JObject parameters)
    {
        var p           = new ToolParams(parameters);
        var volumeName  = p.Get("volume_name");
        var profilePath = p.Get("profile_path");
        bool isGlobal   = p.GetBool("is_global", true);
        var effects     = p.Get("effects", "");

        if (string.IsNullOrEmpty(volumeName))  return new ErrorResponse("'volume_name' required");
        if (string.IsNullOrEmpty(profilePath)) return new ErrorResponse("'profile_path' required (e.g. Assets/Settings/MyProfile.asset)");

        string dir = System.IO.Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        var added = new List<string>();
        if (!string.IsNullOrEmpty(effects))
        {
            foreach (var eff in effects.Split(','))
            {
                switch (eff.Trim().ToLower())
                {
                    case "bloom":               GetOrAdd<Bloom>(profile);               added.Add("Bloom");               break;
                    case "vignette":            GetOrAdd<Vignette>(profile);            added.Add("Vignette");            break;
                    case "coloradjustments":    GetOrAdd<ColorAdjustments>(profile);    added.Add("ColorAdjustments");    break;
                    case "depthoffield":        GetOrAdd<DepthOfField>(profile);        added.Add("DepthOfField");        break;
                    case "chromaticaberration": GetOrAdd<ChromaticAberration>(profile); added.Add("ChromaticAberration"); break;
                    case "motionblur":          GetOrAdd<MotionBlur>(profile);          added.Add("MotionBlur");          break;
                    case "filmgrain":           GetOrAdd<FilmGrain>(profile);           added.Add("FilmGrain");           break;
                    case "tonemapping":         GetOrAdd<Tonemapping>(profile);         added.Add("Tonemapping");         break;
                    case "whitebalance":        GetOrAdd<WhiteBalance>(profile);        added.Add("WhiteBalance");        break;
                }
            }
        }
        AssetDatabase.SaveAssets();

        var go  = new GameObject(volumeName);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = isGlobal;
        vol.profile  = profile;
        Undo.RegisterCreatedObjectUndo(go, $"Create Volume {volumeName}");

        return new SuccessResponse("Volume created", new { volumeName, profilePath, isGlobal, effectsAdded = added });
    }
}

[UnityCliTool(Name = "pp_add_effect", Description = "기존 VolumeProfile에 이펙트 추가. effect_type: Bloom/Vignette/ColorAdjustments/DepthOfField/ChromaticAberration/MotionBlur/FilmGrain/Tonemapping/WhiteBalance", Group = "render")]
public static class PPAddEffectTool
{
    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var c)) c = profile.Add<T>(true);
        return c;
    }

    public static object HandleCommand(JObject parameters)
    {
        var p           = new ToolParams(parameters);
        var profilePath = p.Get("profile_path");
        var volumeName  = p.Get("volume_name");
        var effectType  = p.Get("effect_type");

        if (string.IsNullOrEmpty(effectType)) return new ErrorResponse("'effect_type' required");

        VolumeProfile profile;
        if (!string.IsNullOrEmpty(profilePath))
        {
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) return new ErrorResponse($"VolumeProfile not found: '{profilePath}'");
        }
        else if (!string.IsNullOrEmpty(volumeName))
        {
            var go = GameObject.Find(volumeName);
            if (go == null) return new ErrorResponse($"GameObject '{volumeName}' not found");
            var vol = go.GetComponent<Volume>();
            if (vol == null || vol.profile == null) return new ErrorResponse($"Volume/profile not found on '{volumeName}'");
            profile = vol.profile;
        }
        else
        {
            return new ErrorResponse("'profile_path' or 'volume_name' required");
        }

        VolumeComponent comp;
        switch (effectType.Trim().ToLower())
        {
            case "bloom":               comp = GetOrAdd<Bloom>(profile);               break;
            case "vignette":            comp = GetOrAdd<Vignette>(profile);            break;
            case "coloradjustments":    comp = GetOrAdd<ColorAdjustments>(profile);    break;
            case "depthoffield":        comp = GetOrAdd<DepthOfField>(profile);        break;
            case "chromaticaberration": comp = GetOrAdd<ChromaticAberration>(profile); break;
            case "motionblur":          comp = GetOrAdd<MotionBlur>(profile);          break;
            case "filmgrain":           comp = GetOrAdd<FilmGrain>(profile);           break;
            case "tonemapping":         comp = GetOrAdd<Tonemapping>(profile);         break;
            case "whitebalance":        comp = GetOrAdd<WhiteBalance>(profile);        break;
            default: return new ErrorResponse($"Unknown effect_type '{effectType}'");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Effect added", new { effectType, componentType = comp.GetType().Name });
    }
}

[UnityCliTool(Name = "pp_set", Description = "VolumeProfile의 이펙트 프로퍼티 변경. effect: bloom/color_adjustments/vignette/depth_of_field/chromatic_aberration/motion_blur/film_grain/tonemapping/white_balance", Group = "render")]
public static class PPSetTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p           = new ToolParams(parameters);
        var profilePath = p.Get("profile_path");
        var volumeName  = p.Get("volume_name");
        var effect      = p.Get("effect");
        var property    = p.Get("property");
        var strVal      = p.Get("string_value", "");
        float fltVal    = p.GetFloat("float_value", 0f) ?? 0f;
        bool boolVal    = p.GetBool("bool_value", false);

        if (string.IsNullOrEmpty(effect))   return new ErrorResponse("'effect' required");
        if (string.IsNullOrEmpty(property)) return new ErrorResponse("'property' required");

        VolumeProfile profile;
        if (!string.IsNullOrEmpty(profilePath))
        {
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) return new ErrorResponse($"VolumeProfile not found: '{profilePath}'");
        }
        else if (!string.IsNullOrEmpty(volumeName))
        {
            var go = GameObject.Find(volumeName);
            if (go == null) return new ErrorResponse($"GameObject '{volumeName}' not found");
            var vol = go.GetComponent<Volume>();
            if (vol == null || vol.profile == null) return new ErrorResponse($"Volume/profile not found on '{volumeName}'");
            profile = vol.profile;
        }
        else
        {
            return new ErrorResponse("'profile_path' or 'volume_name' required");
        }

        switch (effect.ToLower())
        {
            case "bloom":
            {
                if (!profile.TryGet<Bloom>(out var b)) return new ErrorResponse("Bloom not found. Use pp_add_effect first.");
                switch (property.ToLower())
                {
                    case "intensity":  b.intensity.overrideState  = true; b.intensity.value  = fltVal; break;
                    case "threshold":  b.threshold.overrideState  = true; b.threshold.value  = fltVal; break;
                    case "scatter":    b.scatter.overrideState    = true; b.scatter.value    = fltVal; break;
                    case "clamp":      b.clamp.overrideState      = true; b.clamp.value      = fltVal; break;
                    default: return new ErrorResponse($"Bloom: unknown property '{property}'. Use intensity/threshold/scatter/clamp");
                }
                break;
            }
            case "color_adjustments":
            {
                if (!profile.TryGet<ColorAdjustments>(out var ca)) return new ErrorResponse("ColorAdjustments not found.");
                switch (property.ToLower())
                {
                    case "postexposure": ca.postExposure.overrideState = true; ca.postExposure.value = fltVal; break;
                    case "contrast":     ca.contrast.overrideState     = true; ca.contrast.value     = fltVal; break;
                    case "saturation":   ca.saturation.overrideState   = true; ca.saturation.value   = fltVal; break;
                    case "hueshift":     ca.hueShift.overrideState     = true; ca.hueShift.value     = fltVal; break;
                    case "colorfilter":
                        if (!ColorUtility.TryParseHtmlString(strVal, out var cf)) return new ErrorResponse($"Invalid color '{strVal}'");
                        ca.colorFilter.overrideState = true; ca.colorFilter.value = cf; break;
                    default: return new ErrorResponse($"ColorAdjustments: unknown property '{property}'");
                }
                break;
            }
            case "vignette":
            {
                if (!profile.TryGet<Vignette>(out var v)) return new ErrorResponse("Vignette not found.");
                switch (property.ToLower())
                {
                    case "intensity":  v.intensity.overrideState  = true; v.intensity.value  = fltVal;  break;
                    case "smoothness": v.smoothness.overrideState = true; v.smoothness.value = fltVal;  break;
                    case "rounded":    v.rounded.overrideState    = true; v.rounded.value    = boolVal; break;
                    case "color":
                        if (!ColorUtility.TryParseHtmlString(strVal, out var vc)) return new ErrorResponse($"Invalid color '{strVal}'");
                        v.color.overrideState = true; v.color.value = vc; break;
                    default: return new ErrorResponse($"Vignette: unknown property '{property}'");
                }
                break;
            }
            case "depth_of_field":
            {
                if (!profile.TryGet<DepthOfField>(out var dof)) return new ErrorResponse("DepthOfField not found.");
                switch (property.ToLower())
                {
                    case "focusdistance": dof.focusDistance.overrideState = true; dof.focusDistance.value = fltVal; break;
                    case "aperture":      dof.aperture.overrideState      = true; dof.aperture.value      = fltVal; break;
                    case "focallength":   dof.focalLength.overrideState   = true; dof.focalLength.value   = fltVal; break;
                    case "mode":
                        if (!Enum.TryParse<DepthOfFieldMode>(strVal, true, out var dm)) return new ErrorResponse($"Invalid DepthOfFieldMode '{strVal}'");
                        dof.mode.overrideState = true; dof.mode.value = dm; break;
                    default: return new ErrorResponse($"DepthOfField: unknown property '{property}'");
                }
                break;
            }
            case "chromatic_aberration":
            {
                if (!profile.TryGet<ChromaticAberration>(out var ca)) return new ErrorResponse("ChromaticAberration not found.");
                switch (property.ToLower())
                {
                    case "intensity": ca.intensity.overrideState = true; ca.intensity.value = fltVal; break;
                    default: return new ErrorResponse($"ChromaticAberration: unknown property '{property}'");
                }
                break;
            }
            case "motion_blur":
            {
                if (!profile.TryGet<MotionBlur>(out var mb)) return new ErrorResponse("MotionBlur not found.");
                switch (property.ToLower())
                {
                    case "intensity":   mb.intensity.overrideState   = true; mb.intensity.value   = fltVal; break;
                    case "clampvalue":  mb.clampValue.overrideState  = true; mb.clampValue.value  = fltVal; break;
                    case "mode":
                        if (!Enum.TryParse<MotionBlurMode>(strVal, true, out var mbm)) return new ErrorResponse($"Invalid MotionBlurMode '{strVal}'. Use CameraOnly or CameraAndObjects");
                        mb.mode.overrideState = true; mb.mode.value = mbm; break;
                    case "quality":
                        if (!Enum.TryParse<MotionBlurQuality>(strVal, true, out var mbq)) return new ErrorResponse($"Invalid MotionBlurQuality '{strVal}'. Use Low/Medium/High");
                        mb.quality.overrideState = true; mb.quality.value = mbq; break;
                    default: return new ErrorResponse($"MotionBlur: unknown property '{property}'. Use intensity/clampValue/mode/quality");
                }
                break;
            }
            case "film_grain":
            {
                if (!profile.TryGet<FilmGrain>(out var fg)) return new ErrorResponse("FilmGrain not found.");
                switch (property.ToLower())
                {
                    case "intensity": fg.intensity.overrideState = true; fg.intensity.value = fltVal; break;
                    case "response":  fg.response.overrideState  = true; fg.response.value  = fltVal; break;
                    default: return new ErrorResponse($"FilmGrain: unknown property '{property}'");
                }
                break;
            }
            case "tonemapping":
            {
                if (!profile.TryGet<Tonemapping>(out var tm)) return new ErrorResponse("Tonemapping not found.");
                switch (property.ToLower())
                {
                    case "mode":
                        if (!Enum.TryParse<TonemappingMode>(strVal, true, out var tmm)) return new ErrorResponse($"Invalid TonemappingMode '{strVal}'");
                        tm.mode.overrideState = true; tm.mode.value = tmm; break;
                    default: return new ErrorResponse($"Tonemapping: unknown property '{property}'");
                }
                break;
            }
            case "white_balance":
            {
                if (!profile.TryGet<WhiteBalance>(out var wb)) return new ErrorResponse("WhiteBalance not found.");
                switch (property.ToLower())
                {
                    case "temperature": wb.temperature.overrideState = true; wb.temperature.value = fltVal; break;
                    case "tint":        wb.tint.overrideState        = true; wb.tint.value        = fltVal; break;
                    default: return new ErrorResponse($"WhiteBalance: unknown property '{property}'");
                }
                break;
            }
            default:
                return new ErrorResponse($"Unknown effect '{effect}'");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Post processing updated", new { effect, property });
    }
}
