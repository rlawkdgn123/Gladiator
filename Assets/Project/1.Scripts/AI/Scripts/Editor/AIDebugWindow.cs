using System.Text;
using Game.Combat.Execution;
using Game.Editor.DebugCLI.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Debug
{
    /// <summary>
    /// 기존 Unity CLI 툴을 버튼 클릭으로 호출하는 디버그 창.
    /// Window > AI Debug > Combat Debug Window
    /// </summary>
    public class AIDebugWindow : EditorWindow
    {
        // ── 자동 갱신 ────────────────────────────────────────
        bool  _autoLog      = false;
        float _autoInterval = 1f;
        float _nextLogTime  = 0f;

        // ── 스크롤 ───────────────────────────────────────────
        Vector2 _scroll;

        // ── 마지막 출력 캐시 (창 내 미리보기용) ────────────────
        string _lastOutput = "";

        // ────────────────────────────────────────────────────
        [MenuItem("Window/AI Debug/Combat Debug Window")]
        public static void Open()
        {
            var win = GetWindow<AIDebugWindow>("Combat AI Debug");
            win.minSize = new Vector2(400, 520);
        }

        void OnEnable()  => EditorApplication.update += OnEditorUpdate;
        void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        void OnEditorUpdate()
        {
            if (!Application.isPlaying || !_autoLog) return;
            if (Time.realtimeSinceStartup < _nextLogTime) return;

            _nextLogTime = Time.realtimeSinceStartup + _autoInterval;
            RunAndLog("internal_state", () => InternalStateAll());
        }

        // ════════════════════════════════════════════════════
        void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚔  Combat AI Debug", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            DrawLine();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode에서만 동작합니다.", MessageType.Info);
                return;
            }

            // ── 자동 갱신 ──────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            _autoLog = EditorGUILayout.ToggleLeft("Auto-log", _autoLog, GUILayout.Width(80));
            EditorGUILayout.LabelField("Interval", GUILayout.Width(52));
            _autoInterval = EditorGUILayout.Slider(_autoInterval, 0.2f, 5f, GUILayout.Width(160));
            EditorGUILayout.LabelField("s", GUILayout.Width(14));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // ── 버튼 그룹 ──────────────────────────────────
            DrawGroup("상태", () =>
            {
                if (BtnWide("internal_state  (EngagePhase / WantsMove / Timer)"))
                    RunAndLog("internal_state", () => InternalStateAll());

                if (BtnWide("combat_snapshot  (HP / GH / Action / Phase)"))
                    RunAndLog("combat_snapshot", () => CallTool(CombatSnapshotTool.HandleCommand));

                if (BtnWide("anim_state_probe  (Animator 파라미터)"))
                    RunAndLog("anim_state_probe", () => CallTool(AnimStateProbeTool.HandleCommand));
            });

            DrawGroup("AI 판단", () =>
            {
                if (BtnWide("action_candidates  (Utility 점수표)"))
                    RunAndLog("action_candidates", () => CallTool(ActionCandidatesTool.HandleCommand));

                if (BtnWide("behavior_trace  (BehaviorTree 실행 경로)"))
                    RunAndLog("behavior_trace", () => CallTool(BehaviorTraceTool.HandleCommand));
            });

            DrawGroup("전투 로그", () =>
            {
                if (BtnWide("combat_runtime_log  (최근 판정 기록)"))
                    RunAndLog("combat_runtime_log", () => CallTool(CombatRuntimeLogTool.HandleCommand));

                if (BtnWide("clear_combat_runtime_log"))
                    RunAndLog("clear_combat_runtime_log", () => CallTool(ClearCombatRuntimeLogTool.HandleCommand));
            });

            DrawGroup("유틸", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Console", GUILayout.Height(24))) ClearConsole();
                if (GUILayout.Button("Refresh", GUILayout.Height(24)))       Repaint();
                EditorGUILayout.EndHorizontal();
            });

            // ── 마지막 출력 미리보기 ───────────────────────
            EditorGUILayout.Space(4);
            DrawLine();
            EditorGUILayout.LabelField("Last Output", EditorStyles.miniLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_lastOutput, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════════
        // 기존 CLI 툴 HandleCommand 호출 → JSON 직렬화
        static string CallTool(System.Func<JObject, object> handler)
        {
            var result = handler(new JObject());
            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        // EnemyCombatController private 필드 노출 (CLI 툴에 없는 데이터)
        static string InternalStateAll()
        {
            var controllers = Object.FindObjectsByType<EnemyCombatController>(FindObjectsSortMode.None);
            if (controllers.Length == 0) return "EnemyCombatController 없음";

            var sb = new StringBuilder();
            foreach (var c in controllers)
            {
                var s = c.GetDebugSnapshot();
                sb.AppendLine($"[{s.ObjectName}]");
                sb.AppendLine($"  EngagementPhase : {s.EngagementPhase}");
                sb.AppendLine($"  dist={s.DistToTarget:F2}  bucket={s.DistanceBucket}");
                sb.AppendLine($"  wantsMove={s.WantsToMove}  isMoving={s.IsMoving}  decTimer={s.DecisionTimer:F3}s");
                sb.AppendLine($"  CombatPhase={s.CombatPhase}  Action={s.CurrentAction}  Guarding={s.IsGuarding}  HP={s.Hp:F0}");
                sb.AppendLine($"  Anim: settled={s.AnimatorSettled}  inTransition={s.AnimatorInTransition}");
                sb.AppendLine($"        isMove={s.AnimIsMoving}  inCombat={s.AnimIsInCombat}");
                sb.AppendLine($"        state={s.AnimatorState}  actionType={s.AnimActionType}  dir={s.AnimDirection}");
            }
            return sb.ToString();
        }

        // ── 공통: 실행 + Debug.Log + 미리보기 캐시 ─────────
        void RunAndLog(string toolName, System.Func<string> fn)
        {
            string output;
            try   { output = fn(); }
            catch (System.Exception e) { output = $"ERROR: {e.Message}"; }

            _lastOutput = output;
            UnityEngine.Debug.Log($"[AI Debug] {toolName}\n{output}");
            Repaint();
        }

        // ── GUI 헬퍼 ────────────────────────────────────────
        static bool BtnWide(string label) =>
            GUILayout.Button(label, GUILayout.Height(24), GUILayout.ExpandWidth(true));

        static void DrawGroup(string title, System.Action body)
        {
            EditorGUILayout.LabelField($"── {title} ─────────────────────────────", EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            body();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        static void DrawLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            EditorGUILayout.Space(2);
        }

        static void ClearConsole()
        {
            var asm    = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            var type   = asm.GetType("UnityEditor.LogEntries");
            var method = type?.GetMethod("Clear");
            method?.Invoke(null, null);
        }
    }
}
