/*
 * 전역 상수 모음
 */
using UnityEngine;

public static class Global
{
    [Tooltip("한 팀의 최대 인원")]
    public const int MaxPlayersPerTeam = 3; // 한 팀 최대 인원

    [Tooltip("Player 레이어 인덱스")]
    public const int PlayerLayer = 6;

    [Tooltip("Enemy 레이어 인덱스")]
    public const int EnemyLayer = 7;

    [Tooltip("Player 레이어 마스크")]
    public const int PlayerLayerMask = 1 << PlayerLayer;

    [Tooltip("Enemy 레이어 마스크")]
    public const int EnemyLayerMask = 1 << EnemyLayer;

}

public static class Pivot
{
    public static readonly Vector2 LeftTop = new Vector2(0f, 1f);
    public static readonly Vector2 CenterTop = new Vector2(0.5f, 1f);
    public static readonly Vector2 RightTop = new Vector2(1f, 1f);

    public static readonly Vector2 LeftCenter = new Vector2(0f, 0.5f);
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 RightCenter = new Vector2(1f, 0.5f);

    public static readonly Vector2 LeftBottom = new Vector2(0f, 0f);
    public static readonly Vector2 CenterBottom = new Vector2(0.5f, 0f);
    public static readonly Vector2 RightBottom = new Vector2(1f, 0f);
}
