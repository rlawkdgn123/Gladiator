using UnityCliConnector;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  Animator CLI Tools
//  POST http://localhost:8090/command  { "command": "<name>", "params": {...} }
// ─────────────────────────────────────────────────────────────────────────────

[UnityCliTool(Name = "animator_create", Description = "AnimatorController asset 생성", Group = "animator")]
public static class AnimatorCreateTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p = new ToolParams(parameters);
        var path = p.Get("path");
        if (string.IsNullOrEmpty(path)) return new ErrorResponse("'path' required (e.g. Assets/Animations/Hero.controller)");

        string dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return new ErrorResponse($"Failed to create controller at '{path}'");

        AssetDatabase.SaveAssets();
        return new SuccessResponse("AnimatorController created", new { path, guid = AssetDatabase.AssetPathToGUID(path) });
    }
}

[UnityCliTool(Name = "animator_info", Description = "AnimatorController 전체 구조 조회 (레이어/스테이트/파라미터/트랜지션)", Group = "animator")]
public static class AnimatorInfoTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p = new ToolParams(parameters);
        var path = p.Get("path");
        if (string.IsNullOrEmpty(path)) return new ErrorResponse("'path' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"AnimatorController not found at '{path}'");

        var paramList = new List<object>();
        foreach (var pm in ctrl.parameters)
            paramList.Add(new { name = pm.name, type = pm.type.ToString(), defaultFloat = pm.defaultFloat, defaultInt = pm.defaultInt, defaultBool = pm.defaultBool });

        var layerList = new List<object>();
        foreach (var layer in ctrl.layers)
        {
            var sm = layer.stateMachine;
            var stateList = new List<object>();
            foreach (var cs in sm.states)
            {
                var s = cs.state;
                var transList = new List<object>();
                foreach (var t in s.transitions)
                {
                    var condList = new List<object>();
                    foreach (var c in t.conditions)
                        condList.Add(new { parameter = c.parameter, mode = c.mode.ToString(), threshold = c.threshold });
                    transList.Add(new { destination = t.destinationState?.name ?? "(exit)", hasExitTime = t.hasExitTime, exitTime = t.exitTime, duration = t.duration, conditions = condList });
                }
                stateList.Add(new { name = s.name, isDefault = sm.defaultState == s, motion = s.motion?.name, speed = s.speed, transitions = transList });
            }
            layerList.Add(new { name = layer.name, weight = layer.defaultWeight, states = stateList });
        }

        return new SuccessResponse("OK", new { path, parameters = paramList, layers = layerList });
    }
}

[UnityCliTool(Name = "animator_add_state", Description = "AnimatorController에 스테이트 추가", Group = "animator")]
public static class AnimatorAddStateTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p = new ToolParams(parameters);
        var path = p.Get("path");
        var name = p.Get("state_name");
        int layerIndex = p.GetInt("layer_index", 0) ?? 0;

        if (string.IsNullOrEmpty(path))  return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(name))  return new ErrorResponse("'state_name' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");
        if (layerIndex >= ctrl.layers.Length) return new ErrorResponse($"layerIndex {layerIndex} out of range (0-{ctrl.layers.Length - 1})");

        var sm = ctrl.layers[layerIndex].stateMachine;
        foreach (var cs in sm.states)
            if (cs.state.name == name) return new ErrorResponse($"State '{name}' already exists in layer {layerIndex}");

        var state = sm.AddState(name);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("State added", new { state = state.name, layerIndex });
    }
}

[UnityCliTool(Name = "animator_set_motion", Description = "스테이트에 AnimationClip 할당", Group = "animator")]
public static class AnimatorSetMotionTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p = new ToolParams(parameters);
        var path    = p.Get("path");
        var state   = p.Get("state_name");
        var clip    = p.Get("clip_path");
        int layer   = p.GetInt("layer_index", 0) ?? 0;

        if (string.IsNullOrEmpty(path))  return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(state)) return new ErrorResponse("'state_name' required");
        if (string.IsNullOrEmpty(clip))  return new ErrorResponse("'clip_path' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");
        var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clip);
        if (animClip == null) return new ErrorResponse($"AnimationClip not found: '{clip}'");
        if (layer >= ctrl.layers.Length) return new ErrorResponse($"layerIndex {layer} out of range");

        var sm = ctrl.layers[layer].stateMachine;
        AnimatorState found = null;
        foreach (var cs in sm.states) if (cs.state.name == state) { found = cs.state; break; }
        if (found == null) return new ErrorResponse($"State '{state}' not found in layer {layer}");

        found.motion = animClip;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Motion set", new { state, clip = animClip.name });
    }
}

[UnityCliTool(Name = "animator_add_param", Description = "AnimatorController에 파라미터 추가 (Float/Int/Bool/Trigger)", Group = "animator")]
public static class AnimatorAddParamTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p    = new ToolParams(parameters);
        var path = p.Get("path");
        var name = p.Get("param_name");
        var type = p.Get("param_type");  // Float | Int | Bool | Trigger

        if (string.IsNullOrEmpty(path)) return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(name)) return new ErrorResponse("'param_name' required");
        if (string.IsNullOrEmpty(type)) return new ErrorResponse("'param_type' required: Float | Int | Bool | Trigger");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");

        foreach (var pm in ctrl.parameters)
            if (pm.name == name) return new ErrorResponse($"Parameter '{name}' already exists");

        AnimatorControllerParameterType pType;
        switch (type.ToLower())
        {
            case "float":   pType = AnimatorControllerParameterType.Float;   break;
            case "int":     pType = AnimatorControllerParameterType.Int;     break;
            case "bool":    pType = AnimatorControllerParameterType.Bool;    break;
            case "trigger": pType = AnimatorControllerParameterType.Trigger; break;
            default: return new ErrorResponse($"Unknown param_type '{type}'. Use Float, Int, Bool or Trigger");
        }

        ctrl.AddParameter(name, pType);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Parameter added", new { name, type });
    }
}

[UnityCliTool(Name = "animator_add_transition", Description = "두 스테이트 사이에 트랜지션 추가", Group = "animator")]
public static class AnimatorAddTransitionTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p         = new ToolParams(parameters);
        var path      = p.Get("path");
        var fromState = p.Get("from_state");
        var toState   = p.Get("to_state");
        int layer     = p.GetInt("layer_index", 0) ?? 0;
        float exitTime  = p.GetFloat("exit_time", 1f) ?? 1f;
        float duration  = p.GetFloat("duration", 0.25f) ?? 0.25f;
        bool hasExitTime = p.GetBool("has_exit_time", true);

        if (string.IsNullOrEmpty(path))      return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(fromState)) return new ErrorResponse("'from_state' required");
        if (string.IsNullOrEmpty(toState))   return new ErrorResponse("'to_state' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");
        if (layer >= ctrl.layers.Length) return new ErrorResponse($"layerIndex {layer} out of range");

        var sm = ctrl.layers[layer].stateMachine;
        AnimatorState from = null, to = null;
        foreach (var cs in sm.states)
        {
            if (cs.state.name == fromState) from = cs.state;
            if (cs.state.name == toState)   to   = cs.state;
        }
        if (from == null) return new ErrorResponse($"State '{fromState}' not found");
        if (to == null)   return new ErrorResponse($"State '{toState}' not found");

        var t = from.AddTransition(to);
        t.hasExitTime = hasExitTime;
        t.exitTime    = exitTime;
        t.duration    = duration;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Transition added", new { from = fromState, to = toState, hasExitTime, exitTime, duration });
    }
}

[UnityCliTool(Name = "animator_set_condition", Description = "트랜지션에 조건 추가", Group = "animator")]
public static class AnimatorSetConditionTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p         = new ToolParams(parameters);
        var path      = p.Get("path");
        var fromState = p.Get("from_state");
        var toState   = p.Get("to_state");
        var paramName = p.Get("param_name");
        var mode      = p.Get("condition_mode");  // Greater|Less|Equals|NotEqual|If|IfNot
        float threshold = p.GetFloat("threshold", 0f) ?? 0f;
        int layer     = p.GetInt("layer_index", 0) ?? 0;

        if (string.IsNullOrEmpty(path))      return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(fromState)) return new ErrorResponse("'from_state' required");
        if (string.IsNullOrEmpty(toState))   return new ErrorResponse("'to_state' required");
        if (string.IsNullOrEmpty(paramName)) return new ErrorResponse("'param_name' required");
        if (string.IsNullOrEmpty(mode))      return new ErrorResponse("'condition_mode' required: Greater|Less|Equals|NotEqual|If|IfNot");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");
        if (layer >= ctrl.layers.Length) return new ErrorResponse($"layerIndex {layer} out of range");

        AnimatorConditionMode condMode;
        switch (mode.ToLower())
        {
            case "greater":  condMode = AnimatorConditionMode.Greater;  break;
            case "less":     condMode = AnimatorConditionMode.Less;     break;
            case "equals":   condMode = AnimatorConditionMode.Equals;   break;
            case "notequal": condMode = AnimatorConditionMode.NotEqual; break;
            case "if":       condMode = AnimatorConditionMode.If;       break;
            case "ifnot":    condMode = AnimatorConditionMode.IfNot;    break;
            default: return new ErrorResponse($"Unknown condition_mode '{mode}'");
        }

        var sm = ctrl.layers[layer].stateMachine;
        AnimatorState from = null;
        foreach (var cs in sm.states)
            if (cs.state.name == fromState) { from = cs.state; break; }
        if (from == null) return new ErrorResponse($"State '{fromState}' not found in layer {layer}");

        bool applied = false;
        foreach (var t in from.transitions)
        {
            if (t.destinationState != null && t.destinationState.name == toState)
            {
                t.AddCondition(condMode, threshold, paramName);
                applied = true;
                break;
            }
        }
        if (!applied) return new ErrorResponse($"No transition from '{fromState}' to '{toState}' found. Use animator_add_transition first.");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Condition added", new { from = fromState, to = toState, paramName, mode, threshold });
    }
}

[UnityCliTool(Name = "animator_set_default", Description = "레이어의 기본(진입) 스테이트 설정", Group = "animator")]
public static class AnimatorSetDefaultTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p     = new ToolParams(parameters);
        var path  = p.Get("path");
        var state = p.Get("state_name");
        int layer = p.GetInt("layer_index", 0) ?? 0;

        if (string.IsNullOrEmpty(path))  return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(state)) return new ErrorResponse("'state_name' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");
        if (layer >= ctrl.layers.Length) return new ErrorResponse($"layerIndex {layer} out of range");

        var sm = ctrl.layers[layer].stateMachine;
        AnimatorState found = null;
        foreach (var cs in sm.states) if (cs.state.name == state) { found = cs.state; break; }
        if (found == null) return new ErrorResponse($"State '{state}' not found in layer {layer}");

        sm.defaultState = found;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Default state set", new { state, layerIndex = layer });
    }
}

[UnityCliTool(Name = "animator_add_layer", Description = "AnimatorController에 레이어 추가", Group = "animator")]
public static class AnimatorAddLayerTool
{
    public static object HandleCommand(JObject parameters)
    {
        var p      = new ToolParams(parameters);
        var path   = p.Get("path");
        var name   = p.Get("layer_name");
        float weight = p.GetFloat("weight", 1f) ?? 1f;

        if (string.IsNullOrEmpty(path)) return new ErrorResponse("'path' required");
        if (string.IsNullOrEmpty(name)) return new ErrorResponse("'layer_name' required");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return new ErrorResponse($"Not found: '{path}'");

        foreach (var l in ctrl.layers)
            if (l.name == name) return new ErrorResponse($"Layer '{name}' already exists");

        ctrl.AddLayer(name);
        var layers = ctrl.layers;
        layers[layers.Length - 1].defaultWeight = weight <= 0f ? 1f : weight;
        ctrl.layers = layers;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return new SuccessResponse("Layer added", new { name, weight, layerIndex = ctrl.layers.Length - 1 });
    }
}
