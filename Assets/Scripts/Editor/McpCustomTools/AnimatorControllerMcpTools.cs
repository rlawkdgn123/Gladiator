using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace Community.Unity.MCP
{
    [McpToolProvider]
    public static class AnimatorControllerMcpTools
    {
        // ──────────────────────────────────────────────
        // Args / Result types
        // ──────────────────────────────────────────────

        [Serializable] public class CreateControllerArgs   { [McpParam("Path to create the AnimatorController asset (e.g. Assets/Animations/MyCtrl.controller)", Required = true)] public string path; }
        [Serializable] public class GetControllerInfoArgs  { [McpParam("Path to the AnimatorController asset", Required = true)] public string path; }
        [Serializable] public class AddStateArgs           { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Name of the new state", Required = true)] public string stateName; [McpParam("Layer index (default 0)")] public int layerIndex; }
        [Serializable] public class SetMotionArgs          { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Name of the state", Required = true)] public string stateName; [McpParam("Path to the AnimationClip asset", Required = true)] public string clipPath; [McpParam("Layer index (default 0)")] public int layerIndex; }
        [Serializable] public class AddParameterArgs       { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Parameter name", Required = true)] public string paramName; [McpParam("Parameter type: Float, Int, Bool, Trigger", Required = true, EnumValues = new[] { "Float", "Int", "Bool", "Trigger" })] public string paramType; }
        [Serializable] public class AddTransitionArgs      { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Source state name", Required = true)] public string fromState; [McpParam("Destination state name", Required = true)] public string toState; [McpParam("Layer index (default 0)")] public int layerIndex; [McpParam("Exit time (0-1, normalized)")] public float exitTime; [McpParam("Transition duration")] public float duration; [McpParam("Whether the transition uses exit time")] public bool hasExitTime; }
        [Serializable] public class SetConditionArgs       { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Source state name", Required = true)] public string fromState; [McpParam("Destination state name", Required = true)] public string toState; [McpParam("Parameter name", Required = true)] public string paramName; [McpParam("Condition mode: Greater, Less, Equals, NotEqual, If, IfNot", Required = true, EnumValues = new[] { "Greater", "Less", "Equals", "NotEqual", "If", "IfNot" })] public string conditionMode; [McpParam("Threshold value for numeric parameters")] public float threshold; [McpParam("Layer index (default 0)")] public int layerIndex; }
        [Serializable] public class SetDefaultStateArgs    { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("State name to set as default", Required = true)] public string stateName; [McpParam("Layer index (default 0)")] public int layerIndex; }
        [Serializable] public class AddLayerArgs           { [McpParam("Path to the AnimatorController asset", Required = true)] public string controllerPath; [McpParam("Name for the new layer", Required = true)] public string layerName; [McpParam("Layer weight (default 1.0)")] public float weight; }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        static AnimatorController LoadController(string path)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            return null;
        }

        // ──────────────────────────────────────────────
        // Tools
        // ──────────────────────────────────────────────

        [McpTool("unity_create_animator_controller", "Creates a new AnimatorController asset at the specified path.", typeof(CreateControllerArgs))]
        public static object CreateAnimatorController(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateControllerArgs>(argsJson);
            if (string.IsNullOrEmpty(args.path))
                return new { error = "path is required." };

            string dir = Path.GetDirectoryName(args.path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(args.path);
            if (ctrl == null)
                return new { error = $"Failed to create AnimatorController at '{args.path}'." };

            AssetDatabase.SaveAssets();
            return new { success = true, path = args.path, guid = AssetDatabase.AssetPathToGUID(args.path) };
        }

        [McpTool("unity_get_animator_controller_info", "Returns the full structure of an AnimatorController (layers, states, parameters, transitions).", typeof(GetControllerInfoArgs))]
        public static object GetAnimatorControllerInfo(string argsJson)
        {
            var args = JsonUtility.FromJson<GetControllerInfoArgs>(argsJson);
            if (string.IsNullOrEmpty(args.path))
                return new { error = "path is required." };

            var ctrl = LoadController(args.path);
            if (ctrl == null)
                return new { error = $"AnimatorController not found at '{args.path}'." };

            // Parameters
            var paramList = new List<object>();
            foreach (var p in ctrl.parameters)
                paramList.Add(new { name = p.name, type = p.type.ToString(), defaultFloat = p.defaultFloat, defaultInt = p.defaultInt, defaultBool = p.defaultBool });

            // Layers
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
                        transList.Add(new
                        {
                            destinationState = t.destinationState != null ? t.destinationState.name : "(exit)",
                            hasExitTime = t.hasExitTime,
                            exitTime = t.exitTime,
                            duration = t.duration,
                            conditions = condList
                        });
                    }
                    stateList.Add(new
                    {
                        name = s.name,
                        isDefault = sm.defaultState == s,
                        motion = s.motion != null ? s.motion.name : null,
                        speed = s.speed,
                        transitions = transList
                    });
                }
                layerList.Add(new { name = layer.name, weight = layer.defaultWeight, states = stateList });
            }

            return new { success = true, path = args.path, parameters = paramList, layers = layerList };
        }

        [McpTool("unity_add_animator_state", "Adds a new state to the specified layer of an AnimatorController.", typeof(AddStateArgs))]
        public static object AddAnimatorState(string argsJson)
        {
            var args = JsonUtility.FromJson<AddStateArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.stateName))      return new { error = "stateName is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };
            if (args.layerIndex < 0 || args.layerIndex >= ctrl.layers.Length)
                return new { error = $"layerIndex {args.layerIndex} is out of range (0-{ctrl.layers.Length - 1})." };

            var sm = ctrl.layers[args.layerIndex].stateMachine;
            if (FindState(sm, args.stateName) != null)
                return new { error = $"State '{args.stateName}' already exists in layer {args.layerIndex}." };

            var state = sm.AddState(args.stateName);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, stateName = state.name, layerIndex = args.layerIndex };
        }

        [McpTool("unity_set_state_motion", "Assigns an AnimationClip to a state in an AnimatorController.", typeof(SetMotionArgs))]
        public static object SetStateMotion(string argsJson)
        {
            var args = JsonUtility.FromJson<SetMotionArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.stateName))      return new { error = "stateName is required." };
            if (string.IsNullOrEmpty(args.clipPath))       return new { error = "clipPath is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(args.clipPath);
            if (clip == null) return new { error = $"AnimationClip not found at '{args.clipPath}'." };

            if (args.layerIndex < 0 || args.layerIndex >= ctrl.layers.Length)
                return new { error = $"layerIndex {args.layerIndex} is out of range." };

            var sm = ctrl.layers[args.layerIndex].stateMachine;
            var state = FindState(sm, args.stateName);
            if (state == null) return new { error = $"State '{args.stateName}' not found in layer {args.layerIndex}." };

            state.motion = clip;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, stateName = args.stateName, clipName = clip.name };
        }

        [McpTool("unity_add_animator_parameter", "Adds a parameter (Float/Int/Bool/Trigger) to an AnimatorController.", typeof(AddParameterArgs))]
        public static object AddAnimatorParameter(string argsJson)
        {
            var args = JsonUtility.FromJson<AddParameterArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.paramName))      return new { error = "paramName is required." };
            if (string.IsNullOrEmpty(args.paramType))      return new { error = "paramType is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };

            foreach (var p in ctrl.parameters)
                if (p.name == args.paramName)
                    return new { error = $"Parameter '{args.paramName}' already exists." };

            AnimatorControllerParameterType pType;
            switch (args.paramType.ToLower())
            {
                case "float":   pType = AnimatorControllerParameterType.Float;   break;
                case "int":     pType = AnimatorControllerParameterType.Int;     break;
                case "bool":    pType = AnimatorControllerParameterType.Bool;    break;
                case "trigger": pType = AnimatorControllerParameterType.Trigger; break;
                default: return new { error = $"Unknown paramType '{args.paramType}'. Use Float, Int, Bool or Trigger." };
            }

            ctrl.AddParameter(args.paramName, pType);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, paramName = args.paramName, paramType = args.paramType };
        }

        [McpTool("unity_add_animator_transition", "Adds a transition between two states in an AnimatorController.", typeof(AddTransitionArgs))]
        public static object AddAnimatorTransition(string argsJson)
        {
            var args = JsonUtility.FromJson<AddTransitionArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.fromState))      return new { error = "fromState is required." };
            if (string.IsNullOrEmpty(args.toState))        return new { error = "toState is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };
            if (args.layerIndex < 0 || args.layerIndex >= ctrl.layers.Length)
                return new { error = $"layerIndex {args.layerIndex} is out of range." };

            var sm = ctrl.layers[args.layerIndex].stateMachine;
            var from = FindState(sm, args.fromState);
            if (from == null) return new { error = $"State '{args.fromState}' not found." };
            var to = FindState(sm, args.toState);
            if (to == null) return new { error = $"State '{args.toState}' not found." };

            var t = from.AddTransition(to);
            t.hasExitTime = args.hasExitTime;
            t.exitTime    = args.exitTime;
            t.duration    = args.duration;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, from = args.fromState, to = args.toState, hasExitTime = t.hasExitTime };
        }

        [McpTool("unity_set_transition_condition", "Adds a condition to a transition between two states.", typeof(SetConditionArgs))]
        public static object SetTransitionCondition(string argsJson)
        {
            var args = JsonUtility.FromJson<SetConditionArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.fromState))      return new { error = "fromState is required." };
            if (string.IsNullOrEmpty(args.toState))        return new { error = "toState is required." };
            if (string.IsNullOrEmpty(args.paramName))      return new { error = "paramName is required." };
            if (string.IsNullOrEmpty(args.conditionMode))  return new { error = "conditionMode is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };
            if (args.layerIndex < 0 || args.layerIndex >= ctrl.layers.Length)
                return new { error = $"layerIndex out of range." };

            var sm = ctrl.layers[args.layerIndex].stateMachine;
            var from = FindState(sm, args.fromState);
            if (from == null) return new { error = $"State '{args.fromState}' not found." };

            AnimatorConditionMode mode;
            switch (args.conditionMode.ToLower())
            {
                case "greater":  mode = AnimatorConditionMode.Greater;  break;
                case "less":     mode = AnimatorConditionMode.Less;     break;
                case "equals":   mode = AnimatorConditionMode.Equals;   break;
                case "notequal": mode = AnimatorConditionMode.NotEqual; break;
                case "if":       mode = AnimatorConditionMode.If;       break;
                case "ifnot":    mode = AnimatorConditionMode.IfNot;    break;
                default: return new { error = $"Unknown conditionMode '{args.conditionMode}'." };
            }

            bool applied = false;
            foreach (var t in from.transitions)
            {
                if (t.destinationState != null && t.destinationState.name == args.toState)
                {
                    t.AddCondition(mode, args.threshold, args.paramName);
                    applied = true;
                    break;
                }
            }

            if (!applied)
                return new { error = $"No transition from '{args.fromState}' to '{args.toState}' found. Add transition first." };

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, from = args.fromState, to = args.toState, paramName = args.paramName, conditionMode = args.conditionMode };
        }

        [McpTool("unity_set_default_state", "Sets the default (entry) state of a layer in an AnimatorController.", typeof(SetDefaultStateArgs))]
        public static object SetDefaultState(string argsJson)
        {
            var args = JsonUtility.FromJson<SetDefaultStateArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.stateName))      return new { error = "stateName is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };
            if (args.layerIndex < 0 || args.layerIndex >= ctrl.layers.Length)
                return new { error = $"layerIndex {args.layerIndex} is out of range." };

            var sm = ctrl.layers[args.layerIndex].stateMachine;
            var state = FindState(sm, args.stateName);
            if (state == null) return new { error = $"State '{args.stateName}' not found in layer {args.layerIndex}." };

            sm.defaultState = state;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, defaultState = args.stateName, layerIndex = args.layerIndex };
        }

        [McpTool("unity_add_animator_layer", "Adds a new layer to an AnimatorController.", typeof(AddLayerArgs))]
        public static object AddAnimatorLayer(string argsJson)
        {
            var args = JsonUtility.FromJson<AddLayerArgs>(argsJson);
            if (string.IsNullOrEmpty(args.controllerPath)) return new { error = "controllerPath is required." };
            if (string.IsNullOrEmpty(args.layerName))      return new { error = "layerName is required." };

            var ctrl = LoadController(args.controllerPath);
            if (ctrl == null) return new { error = $"AnimatorController not found at '{args.controllerPath}'." };

            foreach (var l in ctrl.layers)
                if (l.name == args.layerName)
                    return new { error = $"Layer '{args.layerName}' already exists." };

            float w = args.weight <= 0f ? 1.0f : args.weight;
            ctrl.AddLayer(args.layerName);

            // Set weight on newly added layer
            var layers = ctrl.layers;
            layers[layers.Length - 1].defaultWeight = w;
            ctrl.layers = layers;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return new { success = true, layerName = args.layerName, weight = w, layerIndex = ctrl.layers.Length - 1 };
        }
    }
}
