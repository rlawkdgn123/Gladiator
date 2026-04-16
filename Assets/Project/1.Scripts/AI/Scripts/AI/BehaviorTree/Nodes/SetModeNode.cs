using Game.Core.Enums;

namespace Game.AI.BehaviorTree.Nodes
{
    // Context.Mode 값 설정용 노드.
    public class SetModeNode : BTNode
    {
        readonly AITacticalMode modeToSet;

        public SetModeNode(AITacticalMode modeToSet)
        {
            this.modeToSet = modeToSet;
        }

        public override BTNodeState Evaluate(BTContext context)
        {
            context.Mode = modeToSet;
            context.AddTrace($"SetMode -> {modeToSet}");
            return BTNodeState.Success;
        }
    }
}