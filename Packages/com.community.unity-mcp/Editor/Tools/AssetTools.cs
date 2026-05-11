using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for querying Unity project assets.
    /// </summary>
    [McpToolProvider]
    public class AssetTools
    {
        [McpTool("unity_get_assets", "List assets in a folder", typeof(GetAssetsArgs))]
        public static object GetAssets(string argsJson)
        {
            var args = JsonUtility.FromJson<GetAssetsArgs>(argsJson);
            var folderPath = string.IsNullOrEmpty(args?.folderPath) ? "Assets" : args.folderPath;
            var filter = args?.filter ?? "";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return new { error = $"Invalid folder path: {folderPath}" };
            }

            var guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
            var assets = new List<AssetInfo>();

            // Limit results to prevent huge responses
            var maxResults = 100;
            var count = 0;

            foreach (var guid in guids)
            {
                if (count >= maxResults) break;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                assets.Add(new AssetInfo
                {
                    path = path,
                    name = Path.GetFileName(path),
                    type = type?.Name ?? "Unknown",
                    guid = guid
                });

                count++;
            }

            return new AssetsResult
            {
                folderPath = folderPath,
                filter = filter,
                totalCount = guids.Length,
                returnedCount = assets.Count,
                assets = assets.ToArray()
            };
        }

        private static readonly HashSet<string> ReadableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".shader", ".hlsl", ".cginc", ".compute",
            ".json", ".txt", ".xml", ".asmdef", ".asmref",
            ".uss", ".uxml", ".md", ".yaml", ".yml"
        };

        private const int DefaultMaxBytes = 200 * 1024;
        private const int HardMaxBytes = 2 * 1024 * 1024;

        [McpTool("unity_read_script", "Read the full contents of a text/script asset (whitelisted extensions only). Supports optional line range.", typeof(ReadScriptArgs))]
        public static object ReadScript(string argsJson)
        {
            var args = JsonUtility.FromJson<ReadScriptArgs>(argsJson);
            if (args == null || string.IsNullOrEmpty(args.path))
            {
                return new { error = "path parameter is required (e.g., 'Assets/Project/1.Scripts/Foo.cs')" };
            }

            string normalized = args.path.Replace('\\', '/').TrimStart('/');
            if (!normalized.StartsWith("Assets/") && !normalized.StartsWith("Packages/")
                && normalized != "Assets" && normalized != "Packages")
            {
                return new { error = "path must be inside Assets/ or Packages/" };
            }

            string ext = Path.GetExtension(normalized);
            if (string.IsNullOrEmpty(ext) || !ReadableExtensions.Contains(ext))
            {
                return new { error = $"extension '{ext}' is not in the readable whitelist" };
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            string projectRootFull = Path.GetFullPath(projectRoot);
            if (!fullPath.StartsWith(projectRootFull, StringComparison.OrdinalIgnoreCase))
            {
                return new { error = "resolved path escapes the project root" };
            }

            if (!File.Exists(fullPath))
            {
                return new { error = $"file not found: {normalized}" };
            }

            int maxBytes = args.maxBytes > 0 ? Math.Min(args.maxBytes, HardMaxBytes) : DefaultMaxBytes;

            string fileContent;
            try
            {
                fileContent = File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                return new { error = $"failed to read file: {ex.Message}" };
            }

            string[] allLines = fileContent.Split('\n');
            int totalLines = allLines.Length;

            int startLine = args.startLine > 0 ? args.startLine : 1;
            int endLine = args.endLine > 0 ? args.endLine : totalLines;
            if (startLine > totalLines) startLine = totalLines;
            if (endLine > totalLines) endLine = totalLines;
            if (endLine < startLine) endLine = startLine;

            string content;
            bool rangeApplied = (args.startLine > 0 || args.endLine > 0);
            if (rangeApplied)
            {
                var sb = new StringBuilder();
                for (int i = startLine - 1; i < endLine; i++)
                {
                    sb.Append(allLines[i]);
                    if (i < endLine - 1) sb.Append('\n');
                }
                content = sb.ToString();
            }
            else
            {
                content = fileContent;
            }

            bool truncated = false;
            int originalByteCount = System.Text.Encoding.UTF8.GetByteCount(content);
            if (originalByteCount > maxBytes)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                int safeLen = maxBytes;
                while (safeLen > 0 && (bytes[safeLen] & 0xC0) == 0x80) safeLen--;
                content = System.Text.Encoding.UTF8.GetString(bytes, 0, safeLen);
                truncated = true;
            }

            return new ReadScriptResult
            {
                path = normalized,
                totalLines = totalLines,
                startLine = rangeApplied ? startLine : 1,
                endLine = rangeApplied ? endLine : totalLines,
                byteCount = originalByteCount,
                truncated = truncated,
                content = content
            };
        }

        [McpTool("unity_get_project_settings", "Get Unity project settings")]
        public static object GetProjectSettings(string argsJson)
        {
            string scriptingBackend = "Unknown";
            string apiCompatibility = "Unknown";

            try
            {
                // Use NamedBuildTarget for Unity 2022+ / Unity 6
                var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                scriptingBackend = PlayerSettings.GetScriptingBackend(buildTarget).ToString();
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(buildTarget).ToString();
            }
            catch
            {
                // Fallback for older Unity versions
                scriptingBackend = "N/A";
                apiCompatibility = "N/A";
            }

            return new ProjectSettingsResult
            {
                productName = Application.productName,
                companyName = Application.companyName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scripting = scriptingBackend,
                apiCompatibility = apiCompatibility
            };
        }

        #region Data Types

        [Serializable]
        public class GetAssetsArgs
        {
            [McpParam("Folder path (defaults to 'Assets')")] public string folderPath;
            [McpParam("Filter string (e.g., 't:Prefab', 't:Script', 'MyAsset')")] public string filter;
        }

        [Serializable]
        public class AssetsResult
        {
            public string folderPath;
            public string filter;
            public int totalCount;
            public int returnedCount;
            public AssetInfo[] assets;
        }

        [Serializable]
        public class AssetInfo
        {
            public string path;
            public string name;
            public string type;
            public string guid;
        }

        [Serializable]
        public class ReadScriptArgs
        {
            [McpParam("Asset path under Assets/ or Packages/ (e.g., 'Assets/Project/1.Scripts/Foo.cs')", Required = true)] public string path;
            [McpParam("Optional 1-based start line (inclusive)")] public int startLine;
            [McpParam("Optional 1-based end line (inclusive)")] public int endLine;
            [McpParam("Max bytes to return (default 200KB, hard cap 2MB)")] public int maxBytes;
        }

        [Serializable]
        public class ReadScriptResult
        {
            public string path;
            public int totalLines;
            public int startLine;
            public int endLine;
            public int byteCount;
            public bool truncated;
            public string content;
        }

        [Serializable]
        public class ProjectSettingsResult
        {
            public string productName;
            public string companyName;
            public string version;
            public string unityVersion;
            public string platform;
            public string scripting;
            public string apiCompatibility;
        }

        #endregion
    }
}
