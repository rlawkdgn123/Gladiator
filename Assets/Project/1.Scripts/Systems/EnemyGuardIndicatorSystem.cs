using Game.Combat.Execution;
using GuardIndicatorSystemInfo;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// AI용 가드 인디케이터 시스템
// - 각 인디케이터의 색상/알파 갱신
public class EnemyGuardIndicatorSystem : GaurdIndicatorSystemBase
{
    [SerializeField] public EnemyCombatController m_enemyController;

    protected override void Awake()
    {
        base.Awake();

        if (!m_enemyController)
            m_enemyController = GetComponentInParent<EnemyCombatController>();

        if (!m_enemyController)
            Debug.LogError("[EnemyGuardIndicatorSystem] EnemyCombatController 할당이 되지 않았습니다.");
    }

    protected override void Start()
    {
        base.Start();
    }

    protected void Update()
    {
        GuardIndicatorStateUpdate();
    }

    protected override void GuardIndicatorStateUpdate()
    {
        
        if (Components.GuardIndicators == null)
            return;

        for (int i = 0; i < Components.GuardIndicators.Length; ++i)
        {
            GuardIndicatorBase indicator = Components.GuardIndicators[i];
            if (indicator == null)
                continue;

            Image image = indicator.GetComponent<Image>();
            if (image == null)
                continue;

            switch (indicator.GetHealthState())
            {
                case GuardIndicatorInfo.IndicatorHealthState.Critical: image.color = Color.orangeRed; break;
                case GuardIndicatorInfo.IndicatorHealthState.Destroyed: image.color = Color.black; break;
                default: image.color = Color.green; break;
            }

            switch (indicator.GetActionState())
            {
                case GuardIndicatorInfo.IndicatorActionState.Attack: image.color = Color.darkRed; break;
                case GuardIndicatorInfo.IndicatorActionState.Parry: image.color = Color.orangeRed; break;
                default: break;
            }

            if (States.GuardZone == indicator.GetGuardZone())
                SetOpacity(image, 1f);
            else
                SetOpacity(image, CheckOptions.OffAlpha);
        }
    }
}
