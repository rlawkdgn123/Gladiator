# Gladiator — Claude 작업 가이드

## 프로젝트 스택
- Unity 6 (6000.x), URP 17.3.0
- C#, Behavior Package, Input System
- Git: GitHub (`origin = https://github.com/rlawkdgn123/Gladiator`)

## 폴더 구조
```
Assets/Project/
  0.Scenes/        씬 파일
  1.Scripts/       C# 스크립트
    AI/            AI (BehaviorTree, Brains, Utility, ML)
    Data/
    Editor/
      McpCustomTools/   MCP 커스텀 도구 (McpTool 어트리뷰트)
      UnityCliTools/    CLI 커스텀 도구 (UnityCliTool 어트리뷰트)
    Manager/
    Player/
    Systems/
    UI/
  2.Prefabs/
  3.Arts/
  4.Resources/
    DoubleL/         RPG Animations Pack 원본 애셋
    Models/
    Terrain/
    Unreal/
    UseAnimations/   실사용 애니메이션만 추린 폴더 (GUID 유지하며 분리)
      Attack/        공격 (1Hand_Base_Attack_A_*)
      Guard/         방어 자세 / 방어 피드백 (Shield_Block_Idle/Hit_*)
      Hit/           피격 (Hit_F/L/R_*)
      Parry/         패링 (Shield_Block_Parry_*)
      Move/
        LockOn/      락온 상태 8방향 walk (Shield_Walk_A_*)
        LockOff/     기본 시점 — 대기/달리기/회전 (Stand_Idle / Run / Turn_B_*)
  5.Other/Settings/    URP RenderPipeline asset, Volume 등
Packages/
  com.community.unity-mcp/    임베디드 MCP 서버 (수정 가능)
    Bridge/mcp-bridge.js      Claude Desktop 연결 브릿지
    Editor/Core/              JsonRpcHandler.cs, McpServer.cs
  manifest.json
```

## Git 규칙 (필수)
- **작업 브랜치: `Project/GS` 전용** — 여기서만 commit/push
- `develop`, `master`에 직접 commit/push **절대 금지** — PR만
- 플밍 2명만 commit 권한 (GyeongSeok / 팀원)
- 아트·기획은 pull 전용

## 현재 서버 구성
| 서버 | 포트 | 용도 |
|------|------|------|
| MCP (community unity-mcp) | 3000 | Claude Desktop MCP 도구 79개 |
| CLI (unity-cli-connector) | 8090 | 직접 HTTP POST 호출 |

### CLI 호출 패턴 (PowerShell)
```powershell
Invoke-RestMethod -Uri "http://localhost:8090/command" -Method POST `
  -ContentType "application/json" `
  -Body '{"command":"animator_create","params":{"path":"Assets/..."}}'
```

### CLI 커스텀 도구 목록
`AnimatorCliTools.cs` — `animator_create / info / add_state / set_motion / add_param / add_transition / set_condition / set_default / add_layer`
`RenderCliTools.cs` — `render_get / render_set / pp_get / pp_create_volume / pp_add_effect / pp_set`

## MCP 커스텀 도구 (McpTool 어트리뷰트)
`AnimatorControllerMcpTools.cs` — Animator Controller CRUD
`RenderSettingsMcpTools.cs` — URP PostProcessing (Bloom, ColorAdj, Vignette, MotionBlur 등)
`BehaviorTreeMcpTools.cs` — Behavior 그래프/에이전트 조회·수정

### 패키지 내장 도구 추가분
`Packages/com.community.unity-mcp/Editor/Tools/AssetTools.cs` 의 `unity_read_script` —
Asset 경로의 텍스트/스크립트 파일 전체 읽기. 화이트리스트 확장자
(`.cs/.shader/.hlsl/.cginc/.compute/.json/.txt/.xml/.asmdef/.asmref/.uss/.uxml/.md/.yaml/.yml`)
+ `Assets`/`Packages` 경로 제한 + 옵션 라인 범위(`startLine`/`endLine`) + 200KB 기본 상한 (하드캡 2MB).

### URP MotionBlur 실제 필드명 (URP 17 fd0c0d56fb49)
`mb.mode` / `mb.quality` / `mb.intensity` / `mb.clamp` (clampValue 아님)

## 새 MCP 도구 추가 패턴
```csharp
[McpToolProvider]
public static class MyTools {
    [McpTool("tool_name", "설명", typeof(MyArgs))]
    public static object MyTool(string argsJson) {
        var args = JsonUtility.FromJson<MyArgs>(argsJson);
        // ... Unity API 호출
        return new { success = true };
    }
}
[Serializable]
public class MyArgs {
    [McpParam("설명", Required = true)] public string path;
}
```

## 새 CLI 도구 추가 패턴
```csharp
[UnityCliTool("tool_name", "설명")]
public static class MyCliTool {
    public static object HandleCommand(JObject p) {
        var path = p.Get("path");
        var count = p.GetInt("count", 1);
        // ... Unity API 호출
        return new { success = true };
    }
}
```

## 주의 사항
- PackageCache는 immutable — MCP 서버 수정은 반드시 `Packages/com.community.unity-mcp/` 에서
- Claude Desktop config: `%AppData%\Roaming\Claude\claude_desktop_config.json`
- MCP bridge 수정 후 Claude Desktop 재시작 필요
- Unity API는 반드시 메인 스레드에서 — McpServer의 EnqueueMainThread 사용
- **Claude Code 워크트리 분리 함정**: Claude Code 세션은 `.claude/worktrees/*`에서 동작하지만 Unity는 메인 체크아웃을 봄.
  MCP 서버 코드 수정 등 Unity가 즉시 컴파일해야 하는 작업은 메인 체크아웃에서 직접 작업해야 반영됨 (워크트리 변경은 Unity가 못 봄).
- 애니메이션 클립은 `4.Resources/DoubleL/` (애셋팩 원본)이 아니라 `4.Resources/UseAnimations/` (실사용)에서 가져올 것
