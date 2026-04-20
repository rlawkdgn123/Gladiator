using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatorControllerExporter
{
    [Serializable]
    private class ExportData
    {
        public string controllerName;
        public string assetPath;
        public string exportedAt;
        public List<ParameterData> parameters = new();
        public List<LayerData> layers = new();
    }

    [Serializable]
    private class ParameterData
    {
        public string name;
        public string type;
        public string defaultValue;
    }

    [Serializable]
    private class LayerData
    {
        public string name;
        public float defaultWeight;
        public string blendingMode;
        public string avatarMask;
        public List<StateData> states = new();
        public List<TransitionData> anyStateTransitions = new();
        public List<StateMachineTransitionData> stateMachineTransitions = new();
    }

    [Serializable]
    private class StateData
    {
        public string path;
        public string name;
        public string tag;
        public string motionName;
        public string motionType;
        public float speed;
        public bool writeDefaultValues;
        public List<TransitionData> transitions = new();
    }

    [Serializable]
    private class TransitionData
    {
        public string destination;
        public bool hasExitTime;
        public float exitTime;
        public float duration;
        public float offset;
        public List<ConditionData> conditions = new();
    }

    [Serializable]
    private class StateMachineTransitionData
    {
        public string sourceStateMachine;
        public string destination;
        public List<ConditionData> conditions = new();
    }

    [Serializable]
    private class ConditionData
    {
        public string mode;
        public string parameter;
        public float threshold;
    }

    [MenuItem("Tools/Animations/Export Selected Animator Controller")]
    private static void ExportSelectedAnimatorController()
    {
        AnimatorController animatorController = GetSelectedAnimatorController();
        if (animatorController == null)
        {
            Debug.LogWarning("Animator Controller를 찾지 못했습니다. Animator Controller 에셋이나 Animator가 붙은 오브젝트를 선택하세요.");
            return;
        }

        ExportData exportData = BuildExportData(animatorController);
        string exportDirectory = GetExportDirectory();
        Directory.CreateDirectory(exportDirectory);

        string safeName = SanitizeFileName(animatorController.name);
        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string jsonPath = Path.Combine(exportDirectory, $"{safeName}_{timeStamp}.json");
        string textPath = Path.Combine(exportDirectory, $"{safeName}_{timeStamp}.txt");

        string json = JsonUtility.ToJson(exportData, true);
        string text = BuildTextSummary(exportData);

        File.WriteAllText(jsonPath, json, Encoding.UTF8);
        File.WriteAllText(textPath, text, Encoding.UTF8);

        Debug.Log(
            $"Animator Controller export 완료\n" +
            $"- Controller: {animatorController.name}\n" +
            $"- JSON: {jsonPath}\n" +
            $"- TXT: {textPath}");
    }

    [MenuItem("Tools/Animations/Export Selected Animator Controller", true)]
    private static bool ValidateExportSelectedAnimatorController()
    {
        return GetSelectedAnimatorController() != null;
    }

    private static AnimatorController GetSelectedAnimatorController()
    {
        if (Selection.activeObject is AnimatorController selectedController)
            return selectedController;

        if (Selection.activeGameObject == null)
            return null;

        Animator animator = Selection.activeGameObject.GetComponent<Animator>();
        if (animator == null)
            return null;

        return animator.runtimeAnimatorController as AnimatorController;
    }

    private static ExportData BuildExportData(AnimatorController animatorController)
    {
        ExportData exportData = new()
        {
            controllerName = animatorController.name,
            assetPath = AssetDatabase.GetAssetPath(animatorController),
            exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        foreach (AnimatorControllerParameter parameter in animatorController.parameters)
        {
            exportData.parameters.Add(new ParameterData
            {
                name = parameter.name,
                type = parameter.type.ToString(),
                defaultValue = GetDefaultValue(parameter)
            });
        }

        foreach (AnimatorControllerLayer layer in animatorController.layers)
        {
            LayerData layerData = new()
            {
                name = layer.name,
                defaultWeight = layer.defaultWeight,
                blendingMode = layer.blendingMode.ToString(),
                avatarMask = layer.avatarMask != null ? layer.avatarMask.name : string.Empty
            };

            CollectStateMachine(layer.stateMachine, layer.name, layerData);
            exportData.layers.Add(layerData);
        }

        return exportData;
    }

    private static void CollectStateMachine(
        AnimatorStateMachine stateMachine,
        string currentPath,
        LayerData layerData)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            string statePath = string.IsNullOrEmpty(currentPath)
                ? state.name
                : $"{currentPath}/{state.name}";

            StateData stateData = new()
            {
                path = statePath,
                name = state.name,
                tag = state.tag,
                motionName = state.motion != null ? state.motion.name : string.Empty,
                motionType = state.motion != null ? state.motion.GetType().Name : "None",
                speed = state.speed,
                writeDefaultValues = state.writeDefaultValues
            };

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                stateData.transitions.Add(BuildTransitionData(transition));
            }

            layerData.states.Add(stateData);
        }

        foreach (AnimatorStateTransition anyStateTransition in stateMachine.anyStateTransitions)
        {
            layerData.anyStateTransitions.Add(BuildTransitionData(anyStateTransition));
        }

        foreach (AnimatorTransition stateMachineTransition in stateMachine.entryTransitions)
        {
            layerData.stateMachineTransitions.Add(BuildStateMachineTransitionData(
                currentPath,
                stateMachineTransition));
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            string childPath = string.IsNullOrEmpty(currentPath)
                ? childStateMachine.stateMachine.name
                : $"{currentPath}/{childStateMachine.stateMachine.name}";

            CollectStateMachine(childStateMachine.stateMachine, childPath, layerData);
        }
    }

    private static TransitionData BuildTransitionData(AnimatorStateTransition transition)
    {
        TransitionData transitionData = new()
        {
            destination = GetTransitionDestination(transition),
            hasExitTime = transition.hasExitTime,
            exitTime = transition.exitTime,
            duration = transition.duration,
            offset = transition.offset
        };

        foreach (AnimatorCondition condition in transition.conditions)
        {
            transitionData.conditions.Add(new ConditionData
            {
                mode = condition.mode.ToString(),
                parameter = condition.parameter,
                threshold = condition.threshold
            });
        }

        return transitionData;
    }

    private static StateMachineTransitionData BuildStateMachineTransitionData(
        string sourceStateMachine,
        AnimatorTransition transition)
    {
        StateMachineTransitionData transitionData = new()
        {
            sourceStateMachine = sourceStateMachine,
            destination = GetTransitionDestination(transition)
        };

        foreach (AnimatorCondition condition in transition.conditions)
        {
            transitionData.conditions.Add(new ConditionData
            {
                mode = condition.mode.ToString(),
                parameter = condition.parameter,
                threshold = condition.threshold
            });
        }

        return transitionData;
    }

    private static string GetTransitionDestination(AnimatorTransitionBase transition)
    {
        if (transition is AnimatorStateTransition stateTransition)
        {
            if (stateTransition.destinationState != null)
                return stateTransition.destinationState.name;

            if (stateTransition.destinationStateMachine != null)
                return stateTransition.destinationStateMachine.name;

            if (stateTransition.isExit)
                return "Exit";
        }

        if (transition is AnimatorTransition machineTransition)
        {
            if (machineTransition.destinationState != null)
                return machineTransition.destinationState.name;

            if (machineTransition.destinationStateMachine != null)
                return machineTransition.destinationStateMachine.name;
        }

        return "None";
    }

    private static string GetDefaultValue(AnimatorControllerParameter parameter)
    {
        return parameter.type switch
        {
            AnimatorControllerParameterType.Bool => parameter.defaultBool.ToString(),
            AnimatorControllerParameterType.Float => parameter.defaultFloat.ToString(),
            AnimatorControllerParameterType.Int => parameter.defaultInt.ToString(),
            AnimatorControllerParameterType.Trigger => "Trigger",
            _ => string.Empty,
        };
    }

    private static string BuildTextSummary(ExportData exportData)
    {
        StringBuilder builder = new();

        builder.AppendLine($"Controller: {exportData.controllerName}");
        builder.AppendLine($"AssetPath: {exportData.assetPath}");
        builder.AppendLine($"ExportedAt: {exportData.exportedAt}");
        builder.AppendLine();

        builder.AppendLine("[Parameters]");
        foreach (ParameterData parameter in exportData.parameters)
        {
            builder.AppendLine($"- {parameter.name} ({parameter.type}) = {parameter.defaultValue}");
        }

        builder.AppendLine();

        foreach (LayerData layer in exportData.layers)
        {
            builder.AppendLine($"[Layer] {layer.name}");
            builder.AppendLine($"- DefaultWeight: {layer.defaultWeight}");
            builder.AppendLine($"- BlendingMode: {layer.blendingMode}");
            if (!string.IsNullOrEmpty(layer.avatarMask))
                builder.AppendLine($"- AvatarMask: {layer.avatarMask}");

            if (layer.anyStateTransitions.Count > 0)
            {
                builder.AppendLine("- AnyStateTransitions:");
                foreach (TransitionData transition in layer.anyStateTransitions)
                {
                    AppendTransition(builder, transition, "  ");
                }
            }

            if (layer.stateMachineTransitions.Count > 0)
            {
                builder.AppendLine("- EntryTransitions:");
                foreach (StateMachineTransitionData transition in layer.stateMachineTransitions)
                {
                    builder.AppendLine($"  - {transition.sourceStateMachine} -> {transition.destination}");
                    AppendConditions(builder, transition.conditions, "    ");
                }
            }

            builder.AppendLine("- States:");
            foreach (StateData state in layer.states)
            {
                builder.AppendLine($"  - {state.path}");
                builder.AppendLine($"    Motion: {state.motionName} ({state.motionType})");
                builder.AppendLine($"    Tag: {state.tag}");
                builder.AppendLine($"    Speed: {state.speed}");
                builder.AppendLine($"    WriteDefaults: {state.writeDefaultValues}");

                if (state.transitions.Count > 0)
                {
                    builder.AppendLine("    Transitions:");
                    foreach (TransitionData transition in state.transitions)
                    {
                        AppendTransition(builder, transition, "      ");
                    }
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendTransition(StringBuilder builder, TransitionData transition, string indent)
    {
        builder.AppendLine($"{indent}- To: {transition.destination}");
        builder.AppendLine($"{indent}  HasExitTime: {transition.hasExitTime}, ExitTime: {transition.exitTime}, Duration: {transition.duration}, Offset: {transition.offset}");
        AppendConditions(builder, transition.conditions, $"{indent}  ");
    }

    private static void AppendConditions(StringBuilder builder, List<ConditionData> conditions, string indent)
    {
        if (conditions == null || conditions.Count == 0)
        {
            builder.AppendLine($"{indent}Conditions: None");
            return;
        }

        builder.AppendLine($"{indent}Conditions:");
        foreach (ConditionData condition in conditions)
        {
            builder.AppendLine($"{indent}- {condition.parameter} {condition.mode} {condition.threshold}");
        }
    }

    private static string GetExportDirectory()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, "Temp", "Codex", "AnimatorDumps");
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }
}
