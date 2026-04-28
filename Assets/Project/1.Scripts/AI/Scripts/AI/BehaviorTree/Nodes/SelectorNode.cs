using System.Collections.Generic;

namespace Game.AI.BehaviorTree.Nodes
{

    // 우선순위 분기 만들 때 사용. 한개라도 성공하면 성공시키는 노드임.
    public class SelectorNode : BTNode
    {
        readonly List<BTNode> children;

        public SelectorNode(List<BTNode> children)
        {
            this.children = children;
        }

        public override BTNodeState Evaluate(BTContext context)
        {
            context.AddTrace("Selector : start");

            foreach (var child in children)
            {
                var result = child.Evaluate(context);
                context.AddTrace($"Selector child [{child.GetType().Name}] -> {result}");

                if (result == BTNodeState.Success || result == BTNodeState.Running)
                    return result;
            }

            return BTNodeState.Failure;
        }
    }
}