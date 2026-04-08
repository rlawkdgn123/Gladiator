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

