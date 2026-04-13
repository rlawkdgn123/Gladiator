/*
 * FocusAim
 * 포커스 태세 우선순위 설정을 위한 클래스.
 */
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(CapsuleCollider))]
public class PlayerTargetFinder : MonoBehaviour
{
    [SerializeField] private List<Collider> m_enemyColliders = new List<Collider>();

    private CapsuleCollider m_capsuleCollider;


    private void Awake()
    {
        m_capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        transform.rotation = 
            Quaternion.Euler(
            transform.eulerAngles.x,
            Camera.main.transform.eulerAngles.y,
            transform.eulerAngles.z
        );

        transform.position =
            new Vector3(
            Camera.main.transform.position.x,
            transform.position.y,
            Camera.main.transform.position.z
         );
    }

    private void OnTriggerEnter(Collider other)
    {
        // Enemy 타입만 받음.
        if (!other.CompareTag("Enemy")) return;
        
        if (!m_enemyColliders.Contains(other))
            m_enemyColliders.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (m_enemyColliders.Contains(other))
            m_enemyColliders.Remove(other);
    }

    // 스냅샷(복사본) Getter
    public List<Collider> GetEnemyColliderSnapShot() {
        return new List<Collider>(m_enemyColliders);
    }

    // 초기화
    public void ClearEnemyColliders()
    {
        // null인 모든 collider를 Remove
        m_enemyColliders.RemoveAll(col => col == null);
        m_enemyColliders.Clear();
    }
}
