using System;
using UnityEngine;
using VInspector;

////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////
// 애니메이션 종료
////////////////////////////////////////////////////////////////////////////////////////////////////
/// <summary>
/// 상태머신 추가 관리 클래스 구간입니다.
/// 공격 상태를 빠져나갈 때 PlayerController의 공격 상태도 같이 정리합니다.
/// </summary>
public class PlayerExitStateBehaviour : StateMachineBehaviour
{
    public Action OnStateExitEvent;

    private PlayerController playerController;

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 플레이어 1회 갱신
        if (!playerController)
            playerController = animator.GetComponent<PlayerController>();

        if (!playerController) { return; }

        if (playerController.GetIsAttacking())
            playerController.SetIsAttacking(false);
        if (playerController.GetIsParrying()) // 쳐내기
            playerController.SetIsParrying(false);

        OnStateExitEvent?.Invoke(); // 상태가 끝났다고 외부에 알리는 신호 호출
    }
}
