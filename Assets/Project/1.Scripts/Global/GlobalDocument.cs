/*
 * 전역 상수 모음
 */
using System.Collections.Generic;
using Unity.Multiplayer.Center.Common;
using UnityEngine;

public static class Global
{
    [Tooltip("한 팀의 최대 인원")]
    public const int MaxPlayersPerTeam = 3; // 한 팀 최대 인원

    [Tooltip("Player 레이어 인덱스")]
    public static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
    
    [Tooltip("Enemy 레이어 인덱스")]
    public static readonly int EnemyLayer = LayerMask.NameToLayer("Enemy");

    [Tooltip("Player 레이어 마스크")]
    public static readonly int PlayerLayerMask = 1 << PlayerLayer;

    [Tooltip("Enemy 레이어 마스크")]
    public static readonly int EnemyLayerMask = 1 << EnemyLayer;


    public static readonly HashSet<string> OnCommand = new()
    {
        "On","True","Yes","1",
        "T","t","Y","y",
    };

    public static readonly HashSet<string> OffCommand = new()
    {
        "Off","False","No","0", 
        "F","f","N","n",
    };

    public enum TeamLayer
    { 
        None,
        Player,
        Enemy,
    }

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

/*
 * 직렬화 Attribute 모음
 */

// 직렬화 필드에 붙여 Inspector에서 읽기 전용처럼 보이게 만드는 공용 attribute.
// 일반 런타임 스크립트에서 [DisableField]를 붙여야 하므로, Editor 폴더 밖에 둔다.
public class DisableField : PropertyAttribute
{
    // Play Mode일 때 비활성화할지 여부.
    public readonly bool disableInPlayMode = true;
    // Edit Mode일 때 비활성화할지 여부.
    public readonly bool disableInEditMode = true;

    public DisableField(bool disableInPlayMode = true, bool disableInEditMode = true)
    {
        this.disableInPlayMode = disableInPlayMode;
        this.disableInEditMode = disableInEditMode;
    }
}
