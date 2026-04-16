namespace Game.Core.Enums
{
    // 이거는 나중가서 바뀔 수 있는데 결국 behaviorTree만 쓰는거나 ML만 쓰는거는 없어지게 할 수도 있음.
    public enum BrainType
    {
        None,
        Utility,
        BehaviorTree,
        ML,
        Hybrid
    }
}