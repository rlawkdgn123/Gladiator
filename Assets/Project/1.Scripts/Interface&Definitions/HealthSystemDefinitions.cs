using BodyParts;
using System;
using UnityEngine;
using VInspector;
using static Status.Health;

namespace BodyParts
{
    public enum UnitBodyParts : int
    {
        Head = 0,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
    }

[Serializable]
    public class UnitBodyPart
    {
        [ReadOnly]
        [Range(0f, 100f), Tooltip("부위 체력 비율.")]
        public float HealthPersent;

        [Range(0f, 99999f), Tooltip("부위 최대 체력.")]
        public float MaxHealth;

        [Range(0f, 99999f), Tooltip("부위 현재 체력.")]
        public float CurrentHealth;

        [Tooltip("부위 상태.")]
        public UnitHealthState HealthState = UnitHealthState.Normal;
    }

    [Serializable] public class Head : UnitBodyPart { }
    [Serializable] public class LeftArm : UnitBodyPart { }
    [Serializable] public class RightArm : UnitBodyPart { }
    [Serializable] public class LeftLeg : UnitBodyPart { }
    [Serializable] public class RightLeg : UnitBodyPart { }
}

namespace Status
{
    [Serializable]
    public class Health
    {
        public enum UnitHealthState : int
        {
            Normal = 0,
            Injury,
            Critical,
        }

        [Header("BodyHealth")]
        [ReadOnly]
        [Range(0f, 100f), Tooltip("전체 체력 비율.")]
        public float BodyHealthPersent;

        [ReadOnly]
        [Range(0f, 99999f), Tooltip("전체 체력 총합.")]
        public float MaxBodyHealth;

        [ReadOnly]
        [Range(0f, 99999f), Tooltip("현재 체력 총합.")]
        public float CurrentBodyHealth;


        [Tab("Head")]
        [SerializeField]private BodyParts.Head m_head = new();

        [Tab("LeftArm")]
        [SerializeField] private BodyParts.LeftArm m_leftArm = new();

        [Tab("RightArm")]
        [SerializeField] private BodyParts.RightArm m_rightArm = new();

        [Tab("LeftLeg")]
        [SerializeField] private BodyParts.LeftLeg m_leftLeg = new();

        [Tab("RightLeg")]
        [SerializeField] private BodyParts.RightLeg m_rightLeg = new();

        private BodyParts.Head Head => m_head;
        private BodyParts.LeftArm LeftArm => m_leftArm;
        private BodyParts.RightArm RightArm => m_rightArm;
        private BodyParts.LeftLeg LeftLeg => m_leftLeg;
        private BodyParts.RightLeg RightLeg => m_rightLeg;

        public UnitBodyPart GetBodyParts(UnitBodyParts part)
        {
            switch (part)
            {
                case UnitBodyParts.Head:
                    return Head;
                case UnitBodyParts.LeftArm:
                    return LeftArm;
                case UnitBodyParts.RightArm:
                    return RightArm;
                case UnitBodyParts.LeftLeg:
                    return LeftLeg;
                case UnitBodyParts.RightLeg:
                    return RightLeg;
                default:
                    return null;
            }
        }
    }
}


