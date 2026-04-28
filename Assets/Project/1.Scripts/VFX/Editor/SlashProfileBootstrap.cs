using System.IO;
using Game.VFX.Data;
using UnityEditor;
using UnityEngine;

namespace Game.VFX.Editor
{
    /// <summary>
    /// 검투사 기본 프로파일 에셋을 한 번에 생성.
    /// Window > AI Debug > Create Default Slash Profile
    /// </summary>
    public static class SlashProfileBootstrap
    {
        const string TargetFolder = "Assets/Project/4.Resources/VFX/Profiles";
        const string AssetName    = "Gladiator_Heavy.asset";

        [MenuItem("Window/AI Debug/Create Default Slash Profile")]
        public static void CreateDefault()
        {
            EnsureFolder(TargetFolder);

            string fullPath = $"{TargetFolder}/{AssetName}";
            var existing = AssetDatabase.LoadAssetAtPath<SlashProfileSO>(fullPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                Debug.Log($"[SlashProfile] 이미 존재: {fullPath}");
                return;
            }

            var asset = ScriptableObject.CreateInstance<SlashProfileSO>();
            // 검투사 헤비 톤 (필드 기본값 그대로지만 명시)
            asset.trailCoreColor   = new Color(0.95f, 0.96f, 1f, 1f);
            asset.trailRimColor    = new Color(1.15f, 1.05f, 0.9f, 1f);
            asset.trailLifetime    = 0.22f;
            asset.trailWidthScale  = 1f;
            asset.trailEmission    = 1.2f;
            asset.trailMaxSegments = 24;
            asset.distortionStrength = 0.02f;
            asset.minTipSpeed        = 2.5f;
            asset.hitstopDuration    = 0.055f;
            asset.hitstopScale       = 0.08f;
            asset.shakeAmplitude     = 0.35f;
            asset.shakeDuration      = 0.25f;

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            Debug.Log($"[SlashProfile] 생성: {fullPath}\n각 fighter의 EnemyCombatController.Slash Profile 필드에 이 에셋을 드래그해 할당하세요.");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            // 실제 디렉토리도 생성 (meta 충돌 방지)
            Directory.CreateDirectory(folder);
        }
    }
}
