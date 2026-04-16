using UnityEngine;

// 관중 호응 / 전투 분위기 / 기세 수치
namespace Game.Combat.State
{
    [System.Serializable]
    public class HypeState
    {
        [Range(0f, 100f)]
        public float current = 50f;

        // 기준치 정해두기.
        public bool IsLow => current <= 20f;
        public bool IsHigh => current >= 80f;
    }
}