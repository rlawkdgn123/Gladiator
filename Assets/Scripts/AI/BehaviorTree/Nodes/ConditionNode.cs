using System;

namespace Game.AI.BehaviorTree.Nodes
{
    // 조건에 따라서 BT 결과 반환할거임. 
    public class ConditionNode : BTNode
    {
        readonly string label;
        readonly Func<BTContext, bool> predicate;

        public ConditionNode(string label, Func<BTContext, bool> predicate)
        {
            this.label = label;
            this.predicate = predicate;
        }

        public override BTNodeState Evaluate(BTContext context)
        {
            bool result = predicate(context);
            context.AddTrace($"Condition [{label}] = {result}");

            return result ? BTNodeState.Success : BTNodeState.Failure;
        }
    }
}