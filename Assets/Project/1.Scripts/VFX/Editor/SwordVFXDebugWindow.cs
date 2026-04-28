using Game.Combat.Execution;
using Game.VFX.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.VFX.Editor
{
    /// <summary>
    /// 칼 슬래시 VFX 런타임 디버그·토글 창.
    /// Window > AI Debug > Sword VFX Debug
    ///
    /// Play Mode에서만 의미 있음. 모든 fighter에 대해:
    ///   - Trail/Particles 강제 on/off
    ///   - Impact 수동 트리거
    ///   - Hitstop 수동 트리거
    /// </summary>
    public class SwordVFXDebugWindow : EditorWindow
    {
        [MenuItem("Window/AI Debug/Sword VFX Debug")]
        public static void Open()
        {
            var w = GetWindow<SwordVFXDebugWindow>("Sword VFX");
            w.minSize = new Vector2(360, 420);
        }

        float _impactScale = 1f;
        float _impactLifetime = 0.65f;
        Color _impactTint = new Color(1.1f, 0.95f, 0.75f, 1f);
        float _hitstopDuration = 0.06f;
        float _hitstopScale = 0.08f;

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚔  Sword VFX Debug", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            Line();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode에서만 동작합니다.", MessageType.Info);
                return;
            }

            var controllers = Object.FindObjectsByType<EnemyCombatController>(FindObjectsSortMode.None);
            EditorGUILayout.LabelField($"Fighters in scene: {controllers.Length}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            Group("Hitstop 수동 테스트", () =>
            {
                _hitstopDuration = EditorGUILayout.Slider("Duration (s)", _hitstopDuration, 0f, 0.3f);
                _hitstopScale    = EditorGUILayout.Slider("TimeScale",    _hitstopScale,    0.01f, 1f);
                if (GUILayout.Button("Trigger Hitstop", GUILayout.Height(24)))
                {
                    Hitstop.Request(_hitstopDuration, _hitstopScale);
                }
                EditorGUILayout.LabelField($"Active: {Hitstop.IsActive}", EditorStyles.miniLabel);
            });

            Group("Impact VFX 수동 스폰", () =>
            {
                _impactTint     = EditorGUILayout.ColorField("Tint",     _impactTint);
                _impactScale    = EditorGUILayout.Slider("Scale",       _impactScale,    0.3f, 3f);
                _impactLifetime = EditorGUILayout.Slider("Lifetime (s)",_impactLifetime, 0.2f, 2f);

                if (GUILayout.Button("Spawn at Selected Fighter's Chest", GUILayout.Height(24)))
                {
                    foreach (var c in controllers)
                    {
                        if (c == null) continue;
                        var pos = c.transform.position + Vector3.up * 1.2f;
                        ImpactVFX.Spawn(pos, -c.transform.forward, _impactTint, _impactLifetime, _impactScale);
                    }
                }

                if (GUILayout.Button("Spawn at Camera Forward 3m", GUILayout.Height(24)))
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var pos = cam.transform.position + cam.transform.forward * 3f;
                        ImpactVFX.Spawn(pos, -cam.transform.forward, _impactTint, _impactLifetime, _impactScale);
                    }
                }
            });

            Group("Fighter별 VFX 상태", () =>
            {
                foreach (var c in controllers)
                {
                    if (c == null) continue;
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(c.gameObject.name, GUILayout.Width(160));

                    var vfx = c.GetComponentInChildren<SwordVFXController>();
                    if (vfx == null)
                    {
                        EditorGUILayout.LabelField("SwordVFXController 없음", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            vfx.Profile != null ? $"Profile: {vfx.Profile.name}" : "Profile 없음",
                            EditorStyles.miniLabel);

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                            Selection.activeGameObject = vfx.gameObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });

            EditorGUILayout.Space(6);
            Line();
            EditorGUILayout.LabelField(
                "Tip: 슬래시 셰이더/파라미터는 SlashProfileSO 에셋에서 수정. " +
                "런타임 머티리얼은 Play Mode 종료 시 초기화됨.",
                EditorStyles.wordWrappedMiniLabel);
        }

        static void Group(string title, System.Action body)
        {
            EditorGUILayout.LabelField($"── {title} ─────────────────────────────", EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            body();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        static void Line()
        {
            var r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            EditorGUILayout.Space(2);
        }
    }
}
