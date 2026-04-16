namespace Game.AI.BehaviorTree.Nodes
{
    public abstract class BTNode
    {
        public abstract BTNodeState Evaluate(BTContext context);
    }
}