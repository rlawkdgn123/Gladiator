using System.Collections.Generic;
using Game.Core.Enums;

namespace Game.Core.Types
{
    public class BehaviorTraceResult
    {
        public AITacticalMode Mode;
        public CombatAction Action;
        public List<string> TraceLines = new();
    }
}