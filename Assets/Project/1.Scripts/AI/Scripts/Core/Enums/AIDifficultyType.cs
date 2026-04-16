namespace Game.Core.Enums
{
    // AI 난이도.
    // Easy   : 덜 정확하고 대기를 더 많이 선택
    // Normal : 기본
    // Hard   : 더 정확하게 위급 GH, 프레임 우위, 섹터 다양성을 반영

    public enum AIDifficultyType
    {
        None,
        Easy,
        Normal,
        Hard
    }
}