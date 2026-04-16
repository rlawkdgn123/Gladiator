using UnityEditor;
using UnityEngine;
using Unity.Behavior;
using System;
using System.Collections.Generic;

namespace Community.Unity.MCP
{
    [McpToolProvider]
    public static class BehaviorTreeMcpTools
    {
        // ──────────────────────────────────────────────
        // Args types
        // ──────────────────────────────────────────────

        [Serializable] public class FindGraphsArgs       { [McpParam("Folder to search in (optional, default: Assets)")] public string searchFolder; }
        [Serializable] public class GameObjectNameArgs   { [McpParam("Name of the GameObject in the scene", Required = true)] public string gameObjectName; }
        [Serializable] public class AddAgentArgs         { [McpParam("Name of the GameObject in the scene", Required = true)] public string gameObjectName; [McpParam("Path to the BehaviorGraph asset (optional)")] public string graphPath; }
        [Serializable] public class AssignGraphArgs      { [McpParam("Name of the GameObject in the scene", Required = true)] public string gameObjectName; [McpParam("Path to the BehaviorGraph asset", Required = true)] public string graphPath; }
        [Serializable] public class SetVariableArgs      { [McpParam("Name of the GameObject in the scene", Required = true)] public string gameObjectName; [McpParam("Variable name on the blackboard", Required = true)] public string variableName; [McpParam("Variable type: string, float, int, bool, GameObject", Required = true, EnumValues = new[] { "string", "float", "int", "bool", "GameObject" })] public string variableType; [McpParam("Value as string (used for type=string)")] public string stringValue; [McpParam("Value as float (used for type=float)")] public float floatValue; [McpParam("Value as int (used for type=int)")] public int intValue; [McpParam("Value as bool (used for type=bool)")] public bool boolValue; [McpParam("GameObject name to find and use as value (used for type=GameObject)")] public string gameObjectValue; }
        [Serializable] public class OpenGraphArgs        { [McpParam("Path to the BehaviorGraph asset", Required = true)] public string graphPath; }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        static GameObject FindGameObject(string name)
        {
            return GameObject.Find(name);
        }

        // ──────────────────────────────────────────────
        // Tools
        // ──────────────────────────────────────────────

        [McpTool("unity_find_behavior_graphs", "Finds all BehaviorGraph assets in the project.", typeof(FindGraphsArgs))]
        public static object FindBehaviorGraphs(string argsJson)
        {
            var args = JsonUtility.FromJson<FindGraphsArgs>(argsJson);
            string folder = string.IsNullOrEmpty(args.searchFolder) ? "Assets" : args.searchFolder;

            string[] guids = AssetDatabase.FindAssets("t:BehaviorGraph", new[] { folder });
            var results = new List<object>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                results.Add(new { path, guid, name = asset != null ? asset.name : System.IO.Path.GetFileNameWithoutExtension(path) });
            }

            return new { success = true, count = results.Count, graphs = results };
        }

        [McpTool("unity_get_behavior_agent_info", "Gets information about a BehaviorGraphAgent component on a GameObject.", typeof(GameObjectNameArgs))]
        public static object GetBehaviorAgentInfo(string argsJson)
        {
            var args = JsonUtility.FromJson<GameObjectNameArgs>(argsJson);
            if (string.IsNullOrEmpty(args.gameObjectName))
                return new { error = "gameObjectName is required." };

            var go = FindGameObject(args.gameObjectName);
            if (go == null)
                return new { error = $"GameObject '{args.gameObjectName}' not found in scene." };

            var agent = go.GetComponent<BehaviorGraphAgent>();
            if (agent == null)
                return new { error = $"BehaviorGraphAgent component not found on '{args.gameObjectName}'." };

            string graphName = null;
            string graphPath = null;
            if (agent.Graph != null)
            {
                graphName = agent.Graph.name;
                graphPath = AssetDatabase.GetAssetPath(agent.Graph);
            }

            // Collect blackboard variables
            var varList = new List<object>();
            if (agent.BlackboardReference != null)
            {
                var blackboard = agent.BlackboardReference.Blackboard;
                if (blackboard != null)
                {
                    foreach (var variable in blackboard.Variables)
                    {
                        varList.Add(new
                        {
                            name = variable.Name,
                            type = variable.Type != null ? variable.Type.Name : "unknown"
                        });
                    }
                }
            }

            return new
            {
                success = true,
                gameObjectName = go.name,
                graphName,
                graphPath,
                variableCount = varList.Count,
                variables = varList
            };
        }

        [McpTool("unity_add_behavior_agent", "Adds a BehaviorGraphAgent component to a GameObject and optionally assigns a graph.", typeof(AddAgentArgs))]
        public static object AddBehaviorAgent(string argsJson)
        {
            var args = JsonUtility.FromJson<AddAgentArgs>(argsJson);
            if (string.IsNullOrEmpty(args.gameObjectName))
                return new { error = "gameObjectName is required." };

            var go = FindGameObject(args.gameObjectName);
            if (go == null)
                return new { error = $"GameObject '{args.gameObjectName}' not found in scene." };

            var existing = go.GetComponent<BehaviorGraphAgent>();
            if (existing != null)
                return new { error = $"BehaviorGraphAgent already exists on '{args.gameObjectName}'. Use unity_assign_behavior_graph to change the graph." };

            var agent = go.AddComponent<BehaviorGraphAgent>();
            EditorUtility.SetDirty(go);

            string graphName = null;
            if (!string.IsNullOrEmpty(args.graphPath))
            {
                var graph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(args.graphPath);
                if (graph == null)
                    return new { error = $"BehaviorGraph not found at '{args.graphPath}'." };
                agent.Graph = graph;
                graphName = graph.name;
                EditorUtility.SetDirty(go);
            }

            return new { success = true, gameObjectName = go.name, graphAssigned = graphName != null, graphName };
        }

        [McpTool("unity_assign_behavior_graph", "Assigns or changes the BehaviorGraph on an existing BehaviorGraphAgent.", typeof(AssignGraphArgs))]
        public static object AssignBehaviorGraph(string argsJson)
        {
            var args = JsonUtility.FromJson<AssignGraphArgs>(argsJson);
            if (string.IsNullOrEmpty(args.gameObjectName)) return new { error = "gameObjectName is required." };
            if (string.IsNullOrEmpty(args.graphPath))      return new { error = "graphPath is required." };

            var go = FindGameObject(args.gameObjectName);
            if (go == null) return new { error = $"GameObject '{args.gameObjectName}' not found in scene." };

            var agent = go.GetComponent<BehaviorGraphAgent>();
            if (agent == null)
                return new { error = $"BehaviorGraphAgent not found on '{args.gameObjectName}'. Use unity_add_behavior_agent first." };

            var graph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(args.graphPath);
            if (graph == null) return new { error = $"BehaviorGraph not found at '{args.graphPath}'." };

            agent.Graph = graph;
            EditorUtility.SetDirty(go);

            return new { success = true, gameObjectName = go.name, graphName = graph.name, graphPath = args.graphPath };
        }

        [McpTool("unity_set_behavior_variable", "Sets a blackboard variable value on a BehaviorGraphAgent at runtime.", typeof(SetVariableArgs))]
        public static object SetBehaviorVariable(string argsJson)
        {
            var args = JsonUtility.FromJson<SetVariableArgs>(argsJson);
            if (string.IsNullOrEmpty(args.gameObjectName))  return new { error = "gameObjectName is required." };
            if (string.IsNullOrEmpty(args.variableName))    return new { error = "variableName is required." };
            if (string.IsNullOrEmpty(args.variableType))    return new { error = "variableType is required." };

            if (!Application.isPlaying)
                return new { error = "unity_set_behavior_variable requires Play Mode (runtime). Enter Play Mode first." };

            var go = FindGameObject(args.gameObjectName);
            if (go == null) return new { error = $"GameObject '{args.gameObjectName}' not found in scene." };

            var agent = go.GetComponent<BehaviorGraphAgent>();
            if (agent == null)
                return new { error = $"BehaviorGraphAgent not found on '{args.gameObjectName}'." };

            bool ok = false;
            string setValueStr = null;

            switch (args.variableType.ToLower())
            {
                case "string":
                    ok = agent.SetVariableValue<string>(args.variableName, args.stringValue);
                    setValueStr = args.stringValue;
                    break;
                case "float":
                    ok = agent.SetVariableValue<float>(args.variableName, args.floatValue);
                    setValueStr = args.floatValue.ToString();
                    break;
                case "int":
                    ok = agent.SetVariableValue<int>(args.variableName, args.intValue);
                    setValueStr = args.intValue.ToString();
                    break;
                case "bool":
                    ok = agent.SetVariableValue<bool>(args.variableName, args.boolValue);
                    setValueStr = args.boolValue.ToString();
                    break;
                case "gameobject":
                    if (string.IsNullOrEmpty(args.gameObjectValue))
                        return new { error = "gameObjectValue is required when variableType is GameObject." };
                    var targetGo = FindGameObject(args.gameObjectValue);
                    if (targetGo == null)
                        return new { error = $"Target GameObject '{args.gameObjectValue}' not found in scene." };
                    ok = agent.SetVariableValue<GameObject>(args.variableName, targetGo);
                    setValueStr = args.gameObjectValue;
                    break;
                default:
                    return new { error = $"Unknown variableType '{args.variableType}'. Use string, float, int, bool or GameObject." };
            }

            if (!ok)
                return new { error = $"Failed to set variable '{args.variableName}'. Check that the variable name and type are correct." };

            return new { success = true, gameObjectName = go.name, variableName = args.variableName, variableType = args.variableType, value = setValueStr };
        }

        [McpTool("unity_open_behavior_graph", "Opens the Behavior Graph editor window for a given graph asset.", typeof(OpenGraphArgs))]
        public static object OpenBehaviorGraph(string argsJson)
        {
            var args = JsonUtility.FromJson<OpenGraphArgs>(argsJson);
            if (string.IsNullOrEmpty(args.graphPath))
                return new { error = "graphPath is required." };

            var graph = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(args.graphPath);
            if (graph == null)
                return new { error = $"Asset not found at '{args.graphPath}'." };

            AssetDatabase.OpenAsset(graph);
            return new { success = true, graphPath = args.graphPath, graphName = graph.name };
        }
    }
}
