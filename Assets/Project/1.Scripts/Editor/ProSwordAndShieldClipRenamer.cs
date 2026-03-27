using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProSwordAndShieldClipRenamer
{
    private const string TargetFolderPath = "Assets/Project/3.Arts/Animations/Pro Sword and Shield Pack";

    [MenuItem("Tools/Animations/Rename Pro Sword And Shield Clips")]
    private static void RenameClips()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:Model", new[] { TargetFolderPath });
        int renamedAssetCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
                ModelImporterClipAnimation[] clips = defaultClips.Length > 0 ? defaultClips : importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    continue;
                }

                string baseName = Path.GetFileNameWithoutExtension(assetPath);
                bool changed = false;

                for (int i = 0; i < clips.Length; i++)
                {
                    string targetName = clips.Length == 1 ? baseName : $"{baseName} ({i + 1})";
                    if (clips[i].name == targetName)
                    {
                        continue;
                    }

                    clips[i].name = targetName;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                renamedAssetCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Renamed animation clips in {renamedAssetCount} assets under {TargetFolderPath}.");
    }
}
