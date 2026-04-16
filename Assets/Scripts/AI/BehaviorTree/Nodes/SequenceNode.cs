using System.Collections.Generic;

namespace Game.AI.BehaviorTree.Nodes
{
    // Selector랑 다르게 전부 다 성공해야 성공하는 노드임.
    public class SequenceNode : BTNode
    {
        readonly List<BTNode> children;

        public SequenceNode(List<BTNode> children)
        {
            this.children = children;
        }

        public override BTNodeState Evaluate(BTContext context)
        {
            context.AddTrace("Sequence: start");

            foreach (var child in children)
            {
                var result = child.Evaluate(context);
                context.AddTrace($"Sequence child [{child.GetType().Name}] -> {result}");

                if (result == BTNodeState.Failure)
                    return BTNodeState.Failure;

                if (result == BTNodeState.Running)
                    return BTNodeState.Running;
            }

            return BTNodeState.Success;
        }
    }
}