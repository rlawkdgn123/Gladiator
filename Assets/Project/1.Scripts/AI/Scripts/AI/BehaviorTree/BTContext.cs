using Game.Core.Enums;
using Game.Core.Types;
using System.Collections.Generic;

namespace Game.AI.BehaviorTree
{
    public class BTContext
    {
        public CombatObservation Observation;
        public AITacticalMode Mode = AITacticalMode.None;
        public List<string> TraceLines = new();

        public BTContext(CombatObservation observation)
        {
            Observation = observation;
        }

        public void AddTrace(string message)
        {
            TraceLines.Add(message);
        }
    }
}