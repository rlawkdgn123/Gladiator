namespace Game.Core.Enums
{
    // Aggressive  : 공격 가중치 증가
    // Defensive   : 방어/대기 가중치 증가
    // Default     : 중립

    public enum AIPersonalityType
    {
        None,
        Default,
        Aggressive, 
        Defensive
    }
}