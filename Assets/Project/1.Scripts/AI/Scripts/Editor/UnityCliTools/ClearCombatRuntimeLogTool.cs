using Game.QA.Logging;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace Game.Editor.DebugCLI.Tools
{
    [UnityCliTool(
        Name = "clear_combat_runtime_log",
        Description = "Clears runtime combat logs",
        Group = "combat")]
    public static class ClearCombatRuntimeLogTool
    {
        public static object HandleCommand(JObject parameters)
        {
            CombatRuntimeLogger.Clear();
            return new SuccessResponse("Combat runtime log cleared.");
        }
    }
}