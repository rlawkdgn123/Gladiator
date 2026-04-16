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
| MCP (community unity-mcp) | 3000 | Claude Desktop MCP 도구 55개 |
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
