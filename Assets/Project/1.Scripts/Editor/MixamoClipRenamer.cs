using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MixamoClipRenamer
{
    private static readonly Dictionary<string, string> s_clipNameOverrides = new()
    {
        { "Walk", "Walk Front" },
        { "BackWalk", "Walk Back" },
        { "LeftStrafe", "Left Strafe" },
        { "RightStrafe", "Right Strafe" },
    };

    [MenuItem("Tools/Animations/Normalize Selected Mixamo Clips")]
    private static void NormalizeSelectedMixamoClips()
    {
        string[] modelPaths = CollectSelectedModelPaths();
        if (modelPaths.Length == 0)
        {
            Debug.LogWarning("No model assets selected. Select one or more FBX assets or folders.");
            return;
        }

        int renamedAssetCount = 0;
        StringBuilder logBuilder = new();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string modelPath in modelPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null || !importer.importAnimation)
                {
                    continue;
                }

                ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
                if (sourceClips == null || sourceClips.Length == 0)
                {
                    sourceClips = importer.clipAnimations;
                }

                if (sourceClips == null || sourceClips.Length == 0)
                {
                    logBuilder.AppendLine($"Skipped: {modelPath} (no importable clips found)");
                    continue;
                }

                string baseName = Path.GetFileNameWithoutExtension(modelPath);
                bool changed = false;

                for (int i = 0; i < sourceClips.Length; i++)
                {
                    string targetName = BuildClipName(baseName, i, sourceClips.Length);
                    if (sourceClips[i].name == targetName)
                    {
                        continue;
                    }

                    sourceClips[i].name = targetName;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                importer.clipAnimations = sourceClips;
                importer.SaveAndReimport();
                renamedAssetCount++;
                logBuilder.AppendLine($"Renamed: {modelPath}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        if (renamedAssetCount == 0)
        {
            Debug.Log(logBuilder.Length > 0
                ? $"No clips renamed.\n{logBuilder}"
                : "No clips renamed.");
            return;
        }

        Debug.Log($"Normalized clips in {renamedAssetCount} asset(s).\n{logBuilder}");
    }

    [MenuItem("Tools/Animations/Normalize Selected Mixamo Clips", true)]
    private static bool ValidateNormalizeSelectedMixamoClips()
    {
        return Selection.objects != null && Selection.objects.Length > 0;
    }

    private static string[] CollectSelectedModelPaths()
    {
        HashSet<string> modelPaths = new();

        foreach (Object selectedObject in Selection.objects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                string[] assetGuids = AssetDatabase.FindAssets("t:Model", new[] { assetPath });
                foreach (string assetGuid in assetGuids)
                {
                    modelPaths.Add(AssetDatabase.GUIDToAssetPath(assetGuid));
                }

                continue;
            }

            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(GameObject))
            {
                modelPaths.Add(assetPath);
            }
        }

        string[] results = new string[modelPaths.Count];
        modelPaths.CopyTo(results);
        return results;
    }

    private static string BuildClipName(string baseName, int clipIndex, int clipCount)
    {
        string normalizedBaseName = NormalizeBaseName(baseName);
        if (clipCount == 1)
        {
            return normalizedBaseName;
        }

        return $"{normalizedBaseName} ({clipIndex + 1})";
    }

    private static string NormalizeBaseName(string baseName)
    {
        if (s_clipNameOverrides.TryGetValue(baseName, out string overrideName))
        {
            return overrideName;
        }

        string cleanedName = baseName.Replace("_", " ").Replace("-", " ").Trim();
        if (string.IsNullOrEmpty(cleanedName))
        {
            return baseName;
        }

        StringBuilder builder = new(cleanedName.Length + 8);
        for (int i = 0; i < cleanedName.Length; i++)
        {
            char current = cleanedName[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(cleanedName[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString().Trim();
    }
}
