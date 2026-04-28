using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Combat.Execution;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Debug
{
    /// <summary>
    /// UnityCliTools 폴더의 CLI 툴을 자동으로 수집해서
    /// 에디터 안에서 바로 실행할 수 있게 해주는 디버그 창.
    /// </summary>
    public class AIDebugWindow : EditorWindow
    {
        sealed class ToolEntry
        {
            public string Name;
            public string Group;
            public string EnglishDescription;
            public string KoreanTitle;
            public string KoreanDescription;
            public string Tooltip;
            public string DefaultJson;
            public bool RequiresPlayMode;
            public bool SafeForAutoRun;
            public MethodInfo Handler;

            public string DisplayGroup => string.IsNullOrWhiteSpace(Group) ? "misc" : Group;
        }

        readonly Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _toolOutputs = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _toolStatuses = new Dictionary<string, string>(StringComparer.Ordinal);

        List<ToolEntry> _tools = new List<ToolEntry>();
        Vector2 _toolScroll;
        Vector2 _outputScroll;
        string _selectedToolName = "internal_state";
        string _parameterJson = "{}";
        string _search = "";
        string _lastOutput = "";
        string _groupFilter = "all";

        bool _autoRun;
        float _autoInterval = 1f;
        float _nextAutoRunTime;

        static readonly string[] GroupFilterOrder =
        {
            "all",
            "builtin",
            "combat",
            "animator",
            "render",
            "custom",
            "misc"
        };

        [MenuItem("Window/AI Debug/Combat Debug Window")]
        public static void Open()
        {
            var win = GetWindow<AIDebugWindow>("Combat AI Debug");
            win.minSize = new Vector2(920, 620);
        }

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            RefreshToolCatalog();
            SelectTool(_selectedToolName);
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnFocus()
        {
            RefreshToolCatalog();
        }

        void OnEditorUpdate()
        {
            if (!_autoRun)
                return;

            var tool = GetSelectedTool();
            if (tool == null || !tool.SafeForAutoRun)
                return;

            if (tool.RequiresPlayMode && !Application.isPlaying)
                return;

            if (Time.realtimeSinceStartup < _nextAutoRunTime)
                return;

            _nextAutoRunTime = Time.realtimeSinceStartup + _autoInterval;
            ExecuteTool(tool, _parameterJson, logToConsole: false);
        }

        void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawAutoRunBar();

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawToolCatalog();
                DrawSelectedToolPanel();
            }
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("AI / CLI 툴 디버그 허브", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            EditorGUILayout.LabelField(
                $"현재 상태: {(Application.isPlaying ? "Play Mode" : "Edit Mode")}  |  등록된 툴: {_tools.Count}",
                EditorStyles.miniLabel);

            string modeMessage = Application.isPlaying
                ? "전투/애니메이션/파리 관련 툴은 지금 바로 실행할 수 있습니다."
                : "Edit Mode 입니다. 전투 상태 기반 툴은 Play Mode에서 실행하세요. Animator/Render 편집 툴은 지금도 사용할 수 있습니다.";
            EditorGUILayout.HelpBox(modeMessage, MessageType.Info);
            DrawLine();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("새로고침", "UnityCliTools 폴더의 툴 목록을 다시 스캔합니다."), EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    RefreshToolCatalog();
                }

                if (GUILayout.Button(new GUIContent("콘솔 지우기", "Unity 콘솔 로그를 비웁니다."), EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    ClearConsole();
                }

                GUILayout.Space(8);
                GUILayout.Label("검색", GUILayout.Width(30));
                _search = GUILayout.TextField(_search ?? "", EditorStyles.toolbarTextField, GUILayout.MinWidth(180));

                GUILayout.Space(8);
                GUILayout.Label("그룹", GUILayout.Width(30));
                var filters = BuildGroupFilters();
                int currentIndex = Mathf.Max(0, Array.IndexOf(filters, _groupFilter));
                int nextIndex = EditorGUILayout.Popup(currentIndex, filters, EditorStyles.toolbarPopup, GUILayout.Width(110));
                _groupFilter = filters[nextIndex];

                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent("툴팁은 버튼 위에 마우스를 올리면 보입니다.", "버튼/도움말 아이콘 위에 마우스를 올려 상세 설명을 볼 수 있습니다."), EditorStyles.miniLabel);
            }
        }

        void DrawAutoRunBar()
        {
            var tool = GetSelectedTool();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool disabled = tool == null || !tool.SafeForAutoRun;
                using (new EditorGUI.DisabledScope(disabled))
                {
                    _autoRun = EditorGUILayout.ToggleLeft(
                        new GUIContent("자동 실행", "읽기 전용 툴만 주기적으로 다시 실행합니다."),
                        _autoRun,
                        GUILayout.Width(90));
                }

                EditorGUILayout.LabelField("간격", GUILayout.Width(30));
                _autoInterval = EditorGUILayout.Slider(_autoInterval, 0.25f, 5f, GUILayout.Width(180));
                EditorGUILayout.LabelField("초", GUILayout.Width(20));

                string autoHint = disabled
                    ? "현재 선택한 툴은 자동 실행 대상이 아닙니다."
                    : $"자동 실행 대상: {tool.KoreanTitle}";
                EditorGUILayout.LabelField(autoHint, EditorStyles.miniLabel);
            }
        }

        void DrawToolCatalog()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.42f)))
            {
                EditorGUILayout.LabelField("툴 목록", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("UnityCliTools 폴더에 있는 툴을 자동으로 읽어와서 표시합니다.", EditorStyles.miniLabel);
                EditorGUILayout.Space(4);

                _toolScroll = EditorGUILayout.BeginScrollView(_toolScroll);

                foreach (var group in GetVisibleGroups())
                {
                    if (!_groupFoldouts.ContainsKey(group))
                        _groupFoldouts[group] = true;

                    _groupFoldouts[group] = EditorGUILayout.Foldout(_groupFoldouts[group], ToGroupLabel(group), true);
                    if (!_groupFoldouts[group])
                        continue;

                    foreach (var tool in GetFilteredTools().Where(t => string.Equals(t.DisplayGroup, group, StringComparison.OrdinalIgnoreCase)))
                    {
                        DrawToolRow(tool);
                    }

                    EditorGUILayout.Space(6);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        void DrawToolRow(ToolEntry tool)
        {
            bool isSelected = string.Equals(_selectedToolName, tool.Name, StringComparison.Ordinal);
            var rowStyle = new GUIStyle(EditorStyles.helpBox);
            if (isSelected)
                rowStyle.normal.background = Texture2D.grayTexture;

            using (new EditorGUILayout.VerticalScope(rowStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            new GUIContent(tool.KoreanTitle, tool.Tooltip),
                            GUILayout.Height(26),
                            GUILayout.ExpandWidth(true)))
                    {
                        SelectTool(tool.Name);
                    }

                    using (new EditorGUI.DisabledScope(tool.RequiresPlayMode && !Application.isPlaying))
                    {
                        if (GUILayout.Button(
                                new GUIContent("실행", tool.Tooltip),
                                GUILayout.Width(44),
                                GUILayout.Height(26)))
                        {
                            SelectTool(tool.Name);
                            ExecuteTool(tool, _parameterJson);
                        }
                    }
                }

                EditorGUILayout.LabelField(tool.Name, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(tool.KoreanDescription, EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawSelectedToolPanel()
        {
            var tool = GetSelectedTool();

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("선택한 툴", EditorStyles.boldLabel);

                if (tool == null)
                {
                    EditorGUILayout.HelpBox("실행할 툴을 왼쪽 목록에서 선택하세요.", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(tool.KoreanTitle, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        _toolStatuses.TryGetValue(tool.Name, out var status)
                            ? $"Last Run: {status}"
                            : "Last Run: not executed yet",
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"명령어: {tool.Name}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"그룹: {ToGroupLabel(tool.DisplayGroup)}", EditorStyles.miniLabel);
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField(tool.KoreanDescription, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField($"원본 설명: {tool.EnglishDescription}", EditorStyles.wordWrappedMiniLabel);
                }

                if (tool.RequiresPlayMode && !Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("이 툴은 Play Mode에서 실행하는 것이 안전합니다.", MessageType.Warning);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(new GUIContent("파라미터 JSON", "필요한 값이 있으면 JSON으로 입력하세요. 비워두면 {} 로 실행됩니다."), EditorStyles.boldLabel);
                _parameterJson = EditorGUILayout.TextArea(_parameterJson ?? "{}", GUILayout.MinHeight(140), GUILayout.ExpandHeight(false));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("예시 채우기", "이 툴에 맞는 기본 예시 JSON을 다시 채웁니다."), GUILayout.Height(28)))
                    {
                        _parameterJson = tool.DefaultJson;
                    }

                    if (GUILayout.Button(new GUIContent("예시 복사", "기본 예시 JSON을 클립보드에 복사합니다."), GUILayout.Height(28)))
                    {
                        EditorGUIUtility.systemCopyBuffer = tool.DefaultJson;
                    }

                    using (new EditorGUI.DisabledScope(tool.RequiresPlayMode && !Application.isPlaying))
                    {
                        if (GUILayout.Button(new GUIContent("실행", "현재 입력한 JSON으로 이 툴을 실행합니다."), GUILayout.Height(28)))
                        {
                            ExecuteTool(tool, _parameterJson);
                        }
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("실행 결과", EditorStyles.boldLabel);
                _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll, GUILayout.ExpandHeight(true));
                EditorGUILayout.TextArea(_lastOutput ?? "", EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        void RefreshToolCatalog()
        {
            var discovered = new List<ToolEntry>();

            discovered.Add(CreateInternalStateEntry());

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in GetTypesSafely(assembly))
                {
                    if (type == null || !type.IsClass)
                        continue;

                    if (!string.Equals(type.Namespace, "Game.Editor.DebugCLI.Tools", StringComparison.Ordinal))
                        continue;

                    var handler = type.GetMethod("HandleCommand", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
                    if (handler == null)
                        continue;

                    var cliAttr = type.GetCustomAttributes(false).FirstOrDefault(attr => string.Equals(attr.GetType().Name, "UnityCliToolAttribute", StringComparison.Ordinal));
                    if (cliAttr == null)
                        continue;

                    string name = ReadAttributeString(cliAttr, "Name");
                    string description = ReadAttributeString(cliAttr, "Description");
                    string group = ReadAttributeString(cliAttr, "Group");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    discovered.Add(CreateDiscoveredTool(name, description, group, handler));
                }
            }

            _tools = discovered
                .OrderBy(t => GetGroupSortOrder(t.DisplayGroup))
                .ThenBy(t => t.KoreanTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in _tools.Select(t => t.DisplayGroup).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_groupFoldouts.ContainsKey(group))
                    _groupFoldouts[group] = true;
            }
        }

        ToolEntry CreateInternalStateEntry()
        {
            return new ToolEntry
            {
                Name = "internal_state",
                Group = "builtin",
                EnglishDescription = "Shows current enemy controller state, guard direction, move pressure, and animator state.",
                KoreanTitle = "실시간 내부 상태 보기",
                KoreanDescription = "현재 Engage 상태, 이동 의도, 가드 방향, 애니메이터 상태를 한 번에 확인합니다.",
                Tooltip = "전투 AI의 내부 상태를 한 번에 봅니다. 현재 가드 방향과 읽은 방향도 같이 표시됩니다.",
                DefaultJson = "{}",
                RequiresPlayMode = true,
                SafeForAutoRun = true,
                Handler = null
            };
        }

        ToolEntry CreateDiscoveredTool(string name, string description, string group, MethodInfo handler)
        {
            return new ToolEntry
            {
                Name = name,
                Group = string.IsNullOrWhiteSpace(group) ? "misc" : group,
                EnglishDescription = description ?? "",
                KoreanTitle = GetKoreanTitle(name),
                KoreanDescription = GetKoreanDescription(name, description),
                Tooltip = GetKoreanTooltip(name, description),
                DefaultJson = GetDefaultJson(name),
                RequiresPlayMode = RequiresPlayMode(name, group),
                SafeForAutoRun = IsSafeForAutoRun(name),
                Handler = handler
            };
        }

        void SelectTool(string toolName)
        {
            _selectedToolName = toolName;
            var tool = GetSelectedTool();
            if (tool != null)
            {
                _parameterJson = tool.DefaultJson;
                _lastOutput = _toolOutputs.TryGetValue(tool.Name, out var output) ? output : "";
            }
        }

        ToolEntry GetSelectedTool()
        {
            return _tools.FirstOrDefault(t => string.Equals(t.Name, _selectedToolName, StringComparison.Ordinal));
        }

        IEnumerable<ToolEntry> GetFilteredTools()
        {
            IEnumerable<ToolEntry> tools = _tools;

            if (!string.Equals(_groupFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                tools = tools.Where(t => string.Equals(t.DisplayGroup, _groupFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string key = _search.Trim();
                tools = tools.Where(t =>
                    t.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.KoreanTitle.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.KoreanDescription.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return tools;
        }

        IEnumerable<string> GetVisibleGroups()
        {
            return GetFilteredTools()
                .Select(t => t.DisplayGroup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetGroupSortOrder)
                .ThenBy(g => g, StringComparer.OrdinalIgnoreCase);
        }

        string[] BuildGroupFilters()
        {
            var discovered = _tools.Select(t => t.DisplayGroup).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var ordered = GroupFilterOrder.Where(x => string.Equals(x, "all", StringComparison.OrdinalIgnoreCase) || discovered.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
            foreach (var group in discovered)
            {
                if (!ordered.Contains(group, StringComparer.OrdinalIgnoreCase))
                    ordered.Add(group);
            }

            return ordered.ToArray();
        }

        void ExecuteTool(ToolEntry tool, string jsonText, bool logToConsole = true)
        {
            try
            {
                string output;
                if (string.Equals(tool.Name, "internal_state", StringComparison.Ordinal))
                {
                    output = InternalStateAll();
                }
                else
                {
                    var parameters = ParseJsonObject(jsonText);
                    var result = tool.Handler.Invoke(null, new object[] { parameters });
                    output = JsonConvert.SerializeObject(result, Formatting.Indented);
                }

                _toolOutputs[tool.Name] = output;
                _toolStatuses[tool.Name] = $"{DateTime.Now:HH:mm:ss} success";
                if (string.Equals(_selectedToolName, tool.Name, StringComparison.Ordinal))
                    _lastOutput = output;
                if (logToConsole)
                    UnityEngine.Debug.Log($"[AI Debug] {tool.Name}\n{output}");
                Repaint();
            }
            catch (Exception ex)
            {
                string message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                _toolStatuses[tool.Name] = $"{DateTime.Now:HH:mm:ss} failed";
                _lastOutput = $"실행 중 오류가 발생했습니다.\n{message}";
                _toolOutputs[tool.Name] = _lastOutput;
                UnityEngine.Debug.LogError($"[AI Debug] {tool.Name} failed\n{message}");
                Repaint();
            }
        }

        static JObject ParseJsonObject(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
                return new JObject();

            try
            {
                var token = JToken.Parse(jsonText);
                if (token.Type == JTokenType.Object)
                    return (JObject)token;
                return new JObject();
            }
            catch
            {
                throw new InvalidOperationException("파라미터 JSON 형식이 올바르지 않습니다. 예: {\"steps\": 10}");
            }
        }

        static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        static string ReadAttributeString(object attribute, string propertyName)
        {
            var prop = attribute.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(attribute) as string ?? "";
        }

        static int GetGroupSortOrder(string group)
        {
            int index = Array.FindIndex(GroupFilterOrder, x => string.Equals(x, group, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? index : GroupFilterOrder.Length + 10;
        }

        static string ToGroupLabel(string group)
        {
            return group?.ToLowerInvariant() switch
            {
                "builtin" => "내장 진단",
                "combat" => "전투 AI",
                "animator" => "애니메이터 편집",
                "render" => "렌더 / 포스트프로세싱",
                "custom" => "커스텀",
                _ => "기타"
            };
        }

        static string GetKoreanTitle(string toolName)
        {
            return toolName switch
            {
                "action_candidates" => "행동 후보 점수 보기",
                "anim_state_probe" => "애니메이터 상태 보기",
                "attack_data_probe" => "공격 데이터 보기",
                "behavior_trace" => "행동트리 추적 보기",
                "clear_combat_runtime_log" => "전투 런타임 로그 지우기",
                "combat_runtime_log" => "전투 런타임 로그 보기",
                "combat_snapshot" => "전투 스냅샷 보기",
                "defense_apply_probe" => "방어 적용 결과 보기",
                "defense_resolve_probe" => "방어 판정 결과 보기",
                "parry_learner_probe" => "패링 학습기 보기",
                "ping_game" => "프로젝트 연결 확인",
                "profiling_probe" => "의사결정 프로파일 보기",
                "reward_breakdown" => "보상 분해 보기",
                "run_episode_and_export" => "에피소드 실행 및 내보내기",
                "set_combat_state" => "전투 상태 강제 설정",
                "switch_ai_brain" => "AI 두뇌/성향 변경",
                "animator_create" => "컨트롤러 생성",
                "animator_info" => "컨트롤러 구조 보기",
                "animator_add_state" => "스테이트 추가",
                "animator_set_motion" => "스테이트 모션 지정",
                "animator_add_param" => "애니메이터 파라미터 추가",
                "animator_add_transition" => "트랜지션 추가",
                "animator_set_condition" => "트랜지션 조건 추가",
                "animator_set_default" => "기본 스테이트 설정",
                "animator_add_layer" => "레이어 추가",
                "render_get" => "렌더 설정 조회",
                "render_set" => "렌더 설정 변경",
                "pp_get" => "포스트프로세싱 조회",
                "pp_create_volume" => "볼륨 생성",
                "pp_add_effect" => "볼륨 효과 추가",
                "pp_set" => "볼륨 효과 값 변경",
                _ => toolName
            };
        }

        static string GetKoreanDescription(string toolName, string englishDescription)
        {
            return toolName switch
            {
                "action_candidates" => "현재 상황에서 공격, 가드, 대기 점수가 어떻게 계산되는지 보여줍니다.",
                "anim_state_probe" => "현재 Animator 파라미터와 전이 상태를 빠르게 확인합니다.",
                "attack_data_probe" => "공격 프레임 데이터와 데미지 값을 조회합니다.",
                "behavior_trace" => "BehaviorTree가 어떤 모드와 행동을 골랐는지 추적합니다.",
                "clear_combat_runtime_log" => "누적된 전투 런타임 로그를 비웁니다.",
                "combat_runtime_log" => "최근 전투 판정 로그를 확인합니다.",
                "combat_snapshot" => "HP, 가드 체력, 액션, 페이즈 등 현재 전투 상태를 찍어봅니다.",
                "defense_apply_probe" => "방어 판정 결과를 실제 상태에 적용했을 때 HP/GH가 어떻게 바뀌는지 봅니다.",
                "defense_resolve_probe" => "히트/가드/패링 판정 결과만 미리 계산해서 봅니다.",
                "parry_learner_probe" => "패링 학습 상태를 조회하고, 특정 방향 공격 기록도 넣어볼 수 있습니다.",
                "ping_game" => "현재 프로젝트의 CLI 툴 연결이 살아있는지 확인합니다.",
                "profiling_probe" => "관측, 결정, 실행, 애니메이터 반영 시간까지 측정합니다.",
                "reward_breakdown" => "현재 또는 지정한 행동의 보상 항목을 분해해서 보여줍니다.",
                "run_episode_and_export" => "여러 번 의사결정을 돌리고 결과를 JSON/CSV로 저장합니다.",
                "set_combat_state" => "테스트용으로 전투 상태를 직접 주입합니다.",
                "switch_ai_brain" => "브레인 종류, 성향, 난이도를 즉시 바꿉니다.",
                "animator_create" => "새 AnimatorController 에셋을 만듭니다.",
                "animator_info" => "컨트롤러의 레이어, 상태, 파라미터, 전이를 한 번에 봅니다.",
                "animator_add_state" => "AnimatorController에 새 상태를 추가합니다.",
                "animator_set_motion" => "특정 상태에 애니메이션 클립을 연결합니다.",
                "animator_add_param" => "Float/Int/Bool/Trigger 파라미터를 추가합니다.",
                "animator_add_transition" => "두 상태 사이 전이를 만듭니다.",
                "animator_set_condition" => "전이에 조건을 추가합니다.",
                "animator_set_default" => "레이어의 시작 상태를 지정합니다.",
                "animator_add_layer" => "AnimatorController에 새 레이어를 추가합니다.",
                "render_get" => "현재 씬의 fog, ambient, skybox, sun 설정을 읽어옵니다.",
                "render_set" => "RenderSettings의 한 항목을 수정합니다.",
                "pp_get" => "씬 안의 Volume과 이펙트 목록을 읽어옵니다.",
                "pp_create_volume" => "새 Volume과 VolumeProfile을 만듭니다.",
                "pp_add_effect" => "기존 VolumeProfile에 후처리 효과를 추가합니다.",
                "pp_set" => "후처리 효과 프로퍼티를 수정합니다.",
                _ => string.IsNullOrWhiteSpace(englishDescription) ? "설명 없음" : englishDescription
            };
        }

        static string GetKoreanTooltip(string toolName, string englishDescription)
        {
            return toolName switch
            {
                "action_candidates" => "Utility AI가 각 행동에 몇 점을 주는지 확인합니다. 읽기 전용 툴입니다.",
                "anim_state_probe" => "현재 애니메이터 파라미터와 전이 여부를 확인합니다. Play Mode 권장.",
                "set_combat_state" => "전투 상태를 직접 바꾸는 테스트 툴입니다. 잘못 쓰면 현재 전투 흐름이 바뀝니다.",
                "switch_ai_brain" => "AI의 두뇌, 성향, 난이도를 즉시 변경합니다.",
                "run_episode_and_export" => "반복 실행 후 결과를 파일로 저장합니다. QA/Logging/Exports 아래에 생성됩니다.",
                "render_set" => "RenderSettings 값을 변경합니다. Edit Mode에서도 적용됩니다.",
                "pp_set" => "VolumeProfile 효과 값을 바꿉니다. 잘못된 이름을 넣으면 에러를 반환합니다.",
                _ => GetKoreanDescription(toolName, englishDescription)
            };
        }

        static string GetDefaultJson(string toolName)
        {
            return toolName switch
            {
                "internal_state" => "{}",
                "action_candidates" => "{}",
                "anim_state_probe" => "{}",
                "attack_data_probe" => "{\n  \"action\": \"AttackTopHeavy\"\n}",
                "behavior_trace" => "{}",
                "clear_combat_runtime_log" => "{}",
                "combat_runtime_log" => "{}",
                "combat_snapshot" => "{}",
                "defense_apply_probe" => "{}",
                "defense_resolve_probe" => "{}",
                "parry_learner_probe" => "{\n  \"record\": \"top\"\n}",
                "ping_game" => "{}",
                "profiling_probe" => "{}",
                "reward_breakdown" => "{\n  \"action\": \"GuardLeft\"\n}",
                "run_episode_and_export" => "{\n  \"steps\": 10,\n  \"format\": \"json\"\n}",
                "set_combat_state" => "{\n  \"distanceBucket\": 0,\n  \"hasFrameAdvantage\": false,\n  \"enemy_action\": \"GuardTop\",\n  \"enemy_direction\": \"top\",\n  \"enemy_phase\": \"idle\",\n  \"player_direction\": \"left\",\n  \"recentAttackA\": \"left\"\n}",
                "switch_ai_brain" => "{\n  \"brain\": \"utility\",\n  \"personality\": \"defensive\",\n  \"difficulty\": \"normal\"\n}",
                "animator_create" => "{\n  \"path\": \"Assets/Animations/New.controller\"\n}",
                "animator_info" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\"\n}",
                "animator_add_state" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"state_name\": \"Guard_Ready\",\n  \"layerIndex\": 0\n}",
                "animator_set_motion" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"state_name\": \"Guard_Ready\",\n  \"clip_path\": \"Assets/Animations/Guard.anim\",\n  \"layerIndex\": 0\n}",
                "animator_add_param" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"param_name\": \"IsGuardReady\",\n  \"param_type\": \"Bool\"\n}",
                "animator_add_transition" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"from_state\": \"Guard\",\n  \"to_state\": \"Attack\",\n  \"hasExitTime\": false,\n  \"exitTime\": 0,\n  \"duration\": 0.1,\n  \"layerIndex\": 0\n}",
                "animator_set_condition" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"from_state\": \"Guard\",\n  \"to_state\": \"Attack\",\n  \"param_name\": \"CommitAction\",\n  \"condition_mode\": \"If\",\n  \"threshold\": 0,\n  \"layerIndex\": 0\n}",
                "animator_set_default" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"state_name\": \"Idle\",\n  \"layerIndex\": 0\n}",
                "animator_add_layer" => "{\n  \"path\": \"Assets/Project/2.Prefabs/Enemy/EnemyAI.controller\",\n  \"layer_name\": \"UpperBody\",\n  \"weight\": 1.0\n}",
                "render_get" => "{}",
                "render_set" => "{\n  \"setting\": \"fog_enabled\",\n  \"bool_value\": true\n}",
                "pp_get" => "{\n  \"global_only\": false\n}",
                "pp_create_volume" => "{\n  \"volume_name\": \"DebugVolume\",\n  \"profile_path\": \"Assets/Settings/DebugVolumeProfile.asset\",\n  \"is_global\": true,\n  \"effects\": \"Bloom,Vignette\"\n}",
                "pp_add_effect" => "{\n  \"profile_path\": \"Assets/Settings/DebugVolumeProfile.asset\",\n  \"effect_type\": \"Bloom\"\n}",
                "pp_set" => "{\n  \"profile_path\": \"Assets/Settings/DebugVolumeProfile.asset\",\n  \"effect\": \"bloom\",\n  \"property\": \"intensity\",\n  \"float_value\": 1.2\n}",
                _ => "{}"
            };
        }

        static bool RequiresPlayMode(string toolName, string group)
        {
            if (string.Equals(toolName, "internal_state", StringComparison.Ordinal))
                return true;

            if (string.Equals(group, "animator", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(group, "render", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(toolName, "ping_game", StringComparison.Ordinal))
                return false;

            return string.Equals(group, "combat", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsSafeForAutoRun(string toolName)
        {
            return toolName switch
            {
                "internal_state" => true,
                "action_candidates" => true,
                "anim_state_probe" => true,
                "attack_data_probe" => true,
                "behavior_trace" => true,
                "combat_runtime_log" => true,
                "combat_snapshot" => true,
                "parry_learner_probe" => true,
                "ping_game" => true,
                "profiling_probe" => true,
                "render_get" => true,
                "pp_get" => true,
                _ => false
            };
        }

        static string InternalStateAll()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<EnemyCombatController>(FindObjectsSortMode.None);
            if (controllers.Length == 0)
                return "EnemyCombatController를 찾지 못했습니다.";

            var sb = new StringBuilder();
            foreach (var controller in controllers)
            {
                var s = controller.GetDebugSnapshot();
                sb.AppendLine($"[{s.ObjectName}]");
                sb.AppendLine($"  EngagePhase : {s.EngagementPhase}");
                sb.AppendLine($"  dist={s.DistToTarget:F2}  bucket={s.DistanceBucket}");
                sb.AppendLine($"  wantsMove={s.WantsToMove}  isMoving={s.IsMoving}  moveProb={s.MoveProbability:F2}  forceAdvance={s.ForcedAdvance}  timer={s.DecisionTimer:F3}s");
                sb.AppendLine($"  CombatPhase={s.CombatPhase}  Action={s.CurrentAction}  GuardDir={s.CurrentDirection}  ReadDir={s.ObservedEnemyDirection}  Guarding={s.IsGuarding}");
                sb.AppendLine($"  spacing: attack~mindGame={s.MindGameRange:F2}");
                sb.AppendLine($"  Anim: settled={s.AnimatorSettled}  inTransition={s.AnimatorInTransition}");
                sb.AppendLine($"        isMove={s.AnimIsMoving}  inCombat={s.AnimIsInCombat}");
                sb.AppendLine($"        state={s.AnimatorState}  actionType={s.AnimActionType}  dir={s.AnimDirection}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        static void DrawLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(2f);
        }

        static void ClearConsole()
        {
            var asm = Assembly.GetAssembly(typeof(SceneView));
            var type = asm?.GetType("UnityEditor.LogEntries");
            var method = type?.GetMethod("Clear");
            method?.Invoke(null, null);
        }
    }
}
