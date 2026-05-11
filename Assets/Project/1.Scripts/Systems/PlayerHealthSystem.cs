using UnityEngine;

public class PlayerHealthSystem : HealthSystemBase
{
    [SerializeField] private PlayerController m_playerController;
    protected override void Awake()
    {
        base.Awake();
        m_playerController = GetComponent<PlayerController>();
    }

    protected override void Start()
    {
        base.Start();

    }

    protected override void Update()
    {
        base.Update();
        // 가드존 갱신 코드

    }

    public override void ReceiveHit(HitInfo hitInfo)
    {
        // 피격 상태로 전환
        m_playerController.SetIsAttackReceive(true);


        bool mactchZone = (hitInfo.GuardZone == Values.GuardZone);

        if (!mactchZone)
        {
            m_playerController.Hit();
            return;
        }

        if (m_playerController.GetCanGuard()) // 가드로 바꾸기
            m_playerController.Guard();
        else if (m_playerController.GetIsParrying())
            m_playerController.Parry();
    }
}
